#import <Foundation/Foundation.h>
#import "../Protocol/VSCRProtocol.h"

@class TcpServer;

@protocol TcpServerDelegate <NSObject>
- (void)tcpServer:(TcpServer *)server didClientRequestConfigWithFps:(int)fps bitrate:(int)bitrate scalePct:(int)scalePct forceKeyframe:(BOOL)forceKey;
- (VSCRDeviceInfoPayload)tcpServerRequireDeviceInfo:(TcpServer *)server;
- (void)tcpServerDidClientConnect:(TcpServer *)server clientCount:(NSInteger)count;
- (void)tcpServerDidClientDisconnect:(TcpServer *)server clientCount:(NSInteger)count;
@end

@interface TcpServer : NSObject

@property (nonatomic, weak) id<TcpServerDelegate> delegate;
@property (nonatomic, readonly) BOOL isRunning;
@property (nonatomic, readonly) NSInteger clientCount;

- (instancetype)initWithPort:(int)port;
- (BOOL)start;
- (void)stop;

- (void)broadcastVideoConfigWithSPS:(NSData *)sps pps:(NSData *)pps;
- (void)broadcastVideoFrame:(NSData *)annexBData isKeyframe:(BOOL)isKeyframe frameId:(uint32_t)frameId ptsUs:(uint64_t)ptsUs;

@end
