#import "TcpServer.h"
#import "../Security/AuthManager.h"
#import <sys/socket.h>
#import <netinet/in.h>
#import <netinet/tcp.h>
#import <arpa/inet.h>
#import <unistd.h>
#import <fcntl.h>

@interface ClientContext : NSObject
@property (nonatomic, assign) int socketFd;
@property (nonatomic, assign) BOOL isAuthenticated;
@property (nonatomic, strong) NSMutableData *readBuffer;
@property (nonatomic, strong) dispatch_source_t readSource;
@end

@implementation ClientContext
@end

@implementation TcpServer {
    int _serverPort;
    int _listenFd;
    dispatch_source_t _listenSource;
    dispatch_queue_t _netQueue;
    NSMutableArray<ClientContext *> *_clients;
    NSData *_cachedSps;
    NSData *_cachedPps;
}

- (instancetype)initWithPort:(int)port {
    if ((self = [super init])) {
        _serverPort = (port > 0) ? port : VSCR_DEFAULT_PORT;
        _clients = [NSMutableArray array];
        _netQueue = dispatch_queue_create("com.viewiphone.tcpserver", DISPATCH_QUEUE_SERIAL);
    }
    return self;
}

- (NSInteger)clientCount {
    __block NSInteger count = 0;
    dispatch_sync(_netQueue, ^{
        count = self->_clients.count;
    });
    return count;
}

- (BOOL)start {
    if (_isRunning) return YES;

    _listenFd = socket(AF_INET, SOCK_STREAM, 0);
    if (_listenFd < 0) {
        NSLog(@"[TcpServer] socket() failed: %s", strerror(errno));
        return NO;
    }

    int opt = 1;
    setsockopt(_listenFd, SOL_SOCKET, SO_REUSEADDR, &opt, sizeof(opt));
    setsockopt(_listenFd, SOL_SOCKET, SO_NOSIGPIPE, &opt, sizeof(opt));

    // Set non-blocking
    int flags = fcntl(_listenFd, F_GETFL, 0);
    fcntl(_listenFd, F_SETFL, flags | O_NONBLOCK);

    struct sockaddr_in addr;
    memset(&addr, 0, sizeof(addr));
    addr.sin_family = AF_INET;
    addr.sin_addr.s_addr = htonl(INADDR_ANY);
    addr.sin_port = htons(_serverPort);

    if (bind(_listenFd, (struct sockaddr *)&addr, sizeof(addr)) < 0) {
        NSLog(@"[TcpServer] bind() failed on port %d: %s", _serverPort, strerror(errno));
        close(_listenFd);
        _listenFd = -1;
        return NO;
    }

    if (listen(_listenFd, 16) < 0) {
        NSLog(@"[TcpServer] listen() failed: %s", strerror(errno));
        close(_listenFd);
        _listenFd = -1;
        return NO;
    }

    _listenSource = dispatch_source_create(DISPATCH_SOURCE_TYPE_READ, _listenFd, 0, _netQueue);
    __weak TcpServer *weakSelf = self;
    dispatch_source_set_event_handler(_listenSource, ^{
        [weakSelf _acceptClient];
    });

    dispatch_resume(_listenSource);
    _isRunning = YES;
    NSLog(@"[TcpServer] Server listening on 0.0.0.0:%d", _serverPort);
    return YES;
}

- (void)stop {
    dispatch_sync(_netQueue, ^{
        self->_isRunning = NO;
        if (self->_listenSource) {
            dispatch_source_cancel(self->_listenSource);
            self->_listenSource = NULL;
        }
        if (self->_listenFd >= 0) {
            close(self->_listenFd);
            self->_listenFd = -1;
        }
        for (ClientContext *client in [self->_clients copy]) {
            [self _closeClient:client notify:NO];
        }
        [self->_clients removeAllObjects];
    });
}

- (void)_acceptClient {
    struct sockaddr_in clientAddr;
    socklen_t clientLen = sizeof(clientAddr);
    int clientFd = accept(_listenFd, (struct sockaddr *)&clientAddr, &clientLen);
    if (clientFd < 0) return;

    // Configure client socket
    int opt = 1;
    setsockopt(clientFd, IPPROTO_TCP, TCP_NODELAY, &opt, sizeof(opt));
    setsockopt(clientFd, SOL_SOCKET, SO_NOSIGPIPE, &opt, sizeof(opt));

    int flags = fcntl(clientFd, F_GETFL, 0);
    fcntl(clientFd, F_SETFL, flags | O_NONBLOCK);

    char ipStr[INET_ADDRSTRLEN];
    inet_ntop(AF_INET, &(clientAddr.sin_addr), ipStr, INET_ADDRSTRLEN);
    NSLog(@"[TcpServer] Client connected from %s:%d (fd: %d)", ipStr, ntohs(clientAddr.sin_port), clientFd);

    ClientContext *client = [[ClientContext alloc] init];
    client.socketFd = clientFd;
    client.isAuthenticated = NO;
    client.readBuffer = [NSMutableData data];

    dispatch_source_t readSource = dispatch_source_create(DISPATCH_SOURCE_TYPE_READ, clientFd, 0, _netQueue);
    client.readSource = readSource;

    __weak TcpServer *weakSelf = self;
    __weak ClientContext *weakClient = client;

    dispatch_source_set_event_handler(readSource, ^{
        [weakSelf _handleClientRead:weakClient];
    });

    dispatch_source_set_cancel_handler(readSource, ^{
        if (clientFd >= 0) {
            close(clientFd);
        }
    });

    [_clients addObject:client];
    dispatch_resume(readSource);

    if (self.delegate) {
        [self.delegate tcpServerDidClientConnect:self clientCount:_clients.count];
    }
}

- (void)_handleClientRead:(ClientContext *)client {
    if (!client || client.socketFd < 0) return;

    uint8_t buffer[4096];
    ssize_t bytesRead = read(client.socketFd, buffer, sizeof(buffer));

    if (bytesRead <= 0) {
        if (bytesRead < 0 && (errno == EAGAIN || errno == EWOULDBLOCK)) {
            return;
        }
        // Client disconnected or error
        [self _closeClient:client notify:YES];
        return;
    }

    [client.readBuffer appendBytes:buffer length:bytesRead];
    [self _processIncomingPackets:client];
}

- (void)_processIncomingPackets:(ClientContext *)client {
    while (client.readBuffer.length >= sizeof(VSCRHeader)) {
        const VSCRHeader *hdr = (const VSCRHeader *)client.readBuffer.bytes;

        // Verify magic and version
        if (hdr->magic != VSCR_MAGIC || hdr->version != VSCR_VERSION) {
            NSLog(@"[TcpServer] Invalid packet magic (0x%X) or version (%d), dropping client", hdr->magic, hdr->version);
            [self _closeClient:client notify:YES];
            return;
        }

        uint32_t totalPacketLen = sizeof(VSCRHeader) + hdr->payload_len;
        if (client.readBuffer.length < totalPacketLen) {
            // Need more data to form complete packet
            break;
        }

        NSData *payload = [NSData dataWithBytes:(client.readBuffer.bytes + sizeof(VSCRHeader)) length:hdr->payload_len];
        [self _handlePacketType:hdr->packet_type flags:hdr->flags payload:payload fromClient:client];

        // Advance buffer
        [client.readBuffer replaceBytesInRange:NSMakeRange(0, totalPacketLen) withBytes:NULL length:0];
    }
}

- (void)_handlePacketType:(uint16_t)type flags:(uint16_t)flags payload:(NSData *)payload fromClient:(ClientContext *)client {
    switch (type) {
        case VSCR_PKT_HANDSHAKE: {
            NSLog(@"[TcpServer] Received HANDSHAKE from client %d", client.socketFd);
            // Send back handshake ack
            VSCRHeader resp;
            memset(&resp, 0, sizeof(resp));
            resp.magic = VSCR_MAGIC;
            resp.version = VSCR_VERSION;
            resp.packet_type = VSCR_PKT_HANDSHAKE;
            [self _sendData:[NSData dataWithBytes:&resp length:sizeof(resp)] toClient:client];
            break;
        }

        case VSCR_PKT_AUTH: {
            NSString *token = [[NSString alloc] initWithData:payload encoding:NSUTF8StringEncoding];
            BOOL valid = [[AuthManager sharedManager] validateToken:token];

            VSCRHeader authResp;
            memset(&authResp, 0, sizeof(authResp));
            authResp.magic = VSCR_MAGIC;
            authResp.version = VSCR_VERSION;
            authResp.packet_type = VSCR_PKT_AUTH;
            authResp.flags = valid ? VSCR_FLAG_AUTH_SUCCESS : VSCR_FLAG_AUTH_FAILED;

            [self _sendData:[NSData dataWithBytes:&authResp length:sizeof(authResp)] toClient:client];

            if (valid) {
                client.isAuthenticated = YES;
                NSLog(@"[TcpServer] Client %d authenticated successfully", client.socketFd);

                // Send Device Info
                if (self.delegate) {
                    VSCRDeviceInfoPayload devInfo = [self.delegate tcpServerRequireDeviceInfo:self];
                    [self _sendPacket:VSCR_PKT_DEVICE_INFO flags:0 frameId:0 pts:0 payload:[NSData dataWithBytes:&devInfo length:sizeof(devInfo)] toClient:client];
                }

                // If we have cached SPS/PPS, send VIDEO_CONFIG immediately
                if (_cachedSps && _cachedPps) {
                    [self _sendVideoConfigWithSps:_cachedSps pps:_cachedPps toClient:client];
                }

                // Request keyframe immediately so client can render right away
                if (self.delegate) {
                    [self.delegate tcpServer:self didClientRequestConfigWithFps:0 bitrate:0 scalePct:0 forceKeyframe:YES];
                }
            } else {
                NSLog(@"[TcpServer] Client %d auth failed!", client.socketFd);
                [self _closeClient:client notify:YES];
            }
            break;
        }

        case VSCR_PKT_HEARTBEAT: {
            // Reply Pong
            VSCRHeader pong;
            memset(&pong, 0, sizeof(pong));
            pong.magic = VSCR_MAGIC;
            pong.version = VSCR_VERSION;
            pong.packet_type = VSCR_PKT_HEARTBEAT;
            pong.flags = VSCR_FLAG_PONG;
            [self _sendData:[NSData dataWithBytes:&pong length:sizeof(pong)] toClient:client];
            break;
        }

        case VSCR_PKT_CLIENT_CONFIG: {
            if (payload.length >= sizeof(VSCRClientConfigPayload)) {
                const VSCRClientConfigPayload *cfg = (const VSCRClientConfigPayload *)payload.bytes;
                if (self.delegate) {
                    [self.delegate tcpServer:self 
                        didClientRequestConfigWithFps:cfg->target_fps 
                                              bitrate:cfg->target_bitrate 
                                             scalePct:cfg->target_scale_pct 
                                        forceKeyframe:(cfg->request_keyframe == 1)];
                }
            }
            break;
        }

        case VSCR_PKT_DISCONNECT: {
            [self _closeClient:client notify:YES];
            break;
        }

        default:
            break;
    }
}

- (void)_closeClient:(ClientContext *)client notify:(BOOL)notify {
    if (!client) return;

    if (client.readSource) {
        dispatch_source_cancel(client.readSource);
        client.readSource = NULL;
    }

    [_clients removeObject:client];
    NSLog(@"[TcpServer] Client %d disconnected", client.socketFd);

    if (notify && self.delegate) {
        [self.delegate tcpServerDidClientDisconnect:self clientCount:_clients.count];
    }
}

- (BOOL)_sendData:(NSData *)data toClient:(ClientContext *)client {
    if (!client || client.socketFd < 0 || data.length == 0) return NO;

    const uint8_t *ptr = (const uint8_t *)data.bytes;
    size_t remaining = data.length;

    while (remaining > 0) {
        ssize_t written = write(client.socketFd, ptr, remaining);
        if (written <= 0) {
            if (written < 0 && (errno == EAGAIN || errno == EWOULDBLOCK)) {
                // Buffer full, drop or close
                usleep(1000);
                continue;
            }
            [self _closeClient:client notify:YES];
            return NO;
        }
        ptr += written;
        remaining -= written;
    }
    return YES;
}

- (void)_sendPacket:(uint16_t)type flags:(uint16_t)flags frameId:(uint32_t)frameId pts:(uint64_t)pts payload:(NSData *)payload toClient:(ClientContext *)client {
    VSCRHeader hdr;
    memset(&hdr, 0, sizeof(hdr));
    hdr.magic = VSCR_MAGIC;
    hdr.version = VSCR_VERSION;
    hdr.packet_type = type;
    hdr.flags = flags;
    hdr.frame_id = frameId;
    hdr.timestamp_us = pts;
    hdr.payload_len = (uint32_t)(payload ? payload.length : 0);

    NSMutableData *packet = [NSMutableData dataWithCapacity:sizeof(hdr) + hdr.payload_len];
    [packet appendBytes:&hdr length:sizeof(hdr)];
    if (payload && payload.length > 0) {
        [packet appendData:payload];
    }

    [self _sendData:packet toClient:client];
}

- (void)_sendVideoConfigWithSps:(NSData *)sps pps:(NSData *)pps toClient:(ClientContext *)client {
    if (!client || !client.isAuthenticated) return;

    NSMutableData *configPayload = [NSMutableData dataWithCapacity:8 + sps.length + pps.length];
    uint32_t spsLen = (uint32_t)sps.length;
    uint32_t ppsLen = (uint32_t)pps.length;

    [configPayload appendBytes:&spsLen length:4];
    [configPayload appendData:sps];
    [configPayload appendBytes:&ppsLen length:4];
    [configPayload appendData:pps];

    [self _sendPacket:VSCR_PKT_VIDEO_CONFIG flags:0 frameId:0 pts:0 payload:configPayload toClient:client];
}

- (void)broadcastVideoConfigWithSPS:(NSData *)sps pps:(NSData *)pps {
    dispatch_async(_netQueue, ^{
        self->_cachedSps = [sps copy];
        self->_cachedPps = [pps copy];

        for (ClientContext *client in [self->_clients copy]) {
            if (client.isAuthenticated) {
                [self _sendVideoConfigWithSps:sps pps:pps toClient:client];
            }
        }
    });
}

- (void)broadcastVideoFrame:(NSData *)annexBData isKeyframe:(BOOL)isKeyframe frameId:(uint32_t)frameId ptsUs:(uint64_t)ptsUs {
    dispatch_async(_netQueue, ^{
        if (self->_clients.count == 0) return;

        uint16_t flags = isKeyframe ? VSCR_FLAG_KEYFRAME : VSCR_FLAG_NONE;

        VSCRHeader hdr;
        memset(&hdr, 0, sizeof(hdr));
        hdr.magic = VSCR_MAGIC;
        hdr.version = VSCR_VERSION;
        hdr.packet_type = VSCR_PKT_VIDEO_FRAME;
        hdr.flags = flags;
        hdr.frame_id = frameId;
        hdr.timestamp_us = ptsUs;
        hdr.payload_len = (uint32_t)annexBData.length;

        NSMutableData *packet = [NSMutableData dataWithCapacity:sizeof(hdr) + annexBData.length];
        [packet appendBytes:&hdr length:sizeof(hdr)];
        [packet appendData:annexBData];

        for (ClientContext *client in [self->_clients copy]) {
            if (client.isAuthenticated) {
                [self _sendData:packet toClient:client];
            }
        }
    });
}

@end
