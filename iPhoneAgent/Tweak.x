#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import "Protocol/VSCRProtocol.h"
#import "Capture/ScreenCapture.h"
#import "Encoder/VideoEncoder.h"
#import "Network/TcpServer.h"
#import "DeviceInfo/DeviceInfoHelper.h"
#import "Security/AuthManager.h"

@interface AgentController : NSObject <ScreenCaptureDelegate, VideoEncoderDelegate, TcpServerDelegate>
+ (instancetype)sharedController;
- (void)start;
@end

@implementation AgentController {
    TcpServer *_server;
    ScreenCapture *_capture;
    VideoEncoder *_encoder;
    uint32_t _frameSequence;
    int _currentFps;
    CGFloat _currentScale;
    int _currentBitrate;
}

+ (instancetype)sharedController {
    static AgentController *instance = nil;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        instance = [[AgentController alloc] init];
    });
    return instance;
}

- (instancetype)init {
    if ((self = [super init])) {
        _currentFps = 15;
        _currentScale = 0.5f;
        _currentBitrate = 1500 * 1000;
        _frameSequence = 0;
        _server = [[TcpServer alloc] initWithPort:VSCR_DEFAULT_PORT];
        _server.delegate = self;
    }
    return self;
}

- (void)start {
    NSLog(@"[iPhoneAgent] Starting AgentController in SpringBoard...");
    [_server start];
}

- (void)_startCaptureAndEncoderPipeline {
    if (_capture.isRunning && _encoder.isRunning) return;

    NSLog(@"[iPhoneAgent] Starting capture pipeline: FPS=%d, Scale=%.2f, Bitrate=%d", _currentFps, _currentScale, _currentBitrate);

    _capture = [[ScreenCapture alloc] initWithFps:_currentFps scale:_currentScale];
    _capture.delegate = self;

    _encoder = [[VideoEncoder alloc] init];
    _encoder.bitrate = _currentBitrate;
    _encoder.delegate = self;

    CGSize size = _capture.displaySize;
    BOOL encOk = [_encoder startWithWidth:(int)size.width height:(int)size.height fps:_currentFps];
    if (!encOk) {
        NSLog(@"[iPhoneAgent] Failed to start VideoEncoder");
        return;
    }

    if (![_capture start]) {
        NSLog(@"[iPhoneAgent] Failed to start ScreenCapture: %@", _capture.lastError);
        [_encoder stop];
        return;
    }

    NSLog(@"[iPhoneAgent] Pipeline successfully started with resolution: %dx%d", (int)size.width, (int)size.height);
}

- (void)_stopCaptureAndEncoderPipeline {
    NSLog(@"[iPhoneAgent] Stopping capture pipeline (no active clients to save battery)");
    [_capture stop];
    _capture = nil;
    [_encoder stop];
    _encoder = nil;
}

#pragma mark - ScreenCaptureDelegate

- (void)screenCapture:(ScreenCapture *)capture didCaptureBuffer:(CVPixelBufferRef)pixelBuffer pts:(uint64_t)ptsUs {
    if (_encoder) {
        [_encoder encodePixelBuffer:pixelBuffer pts:ptsUs];
    }
}

#pragma mark - VideoEncoderDelegate

- (void)videoEncoder:(VideoEncoder *)encoder didProduceSPS:(NSData *)sps pps:(NSData *)pps {
    [_server broadcastVideoConfigWithSPS:sps pps:pps];
}

- (void)videoEncoder:(VideoEncoder *)encoder didProduceAnnexBFrame:(NSData *)frameData isKeyframe:(BOOL)isKeyframe pts:(uint64_t)ptsUs {
    _frameSequence++;
    [_server broadcastVideoFrame:frameData isKeyframe:isKeyframe frameId:_frameSequence ptsUs:ptsUs];
}

#pragma mark - TcpServerDelegate

- (void)tcpServer:(TcpServer *)server didClientRequestConfigWithFps:(int)fps bitrate:(int)bitrate scalePct:(int)scalePct forceKeyframe:(BOOL)forceKey {
    BOOL restartPipeline = NO;

    if (fps >= 10 && fps <= 60 && fps != _currentFps) {
        _currentFps = fps;
        restartPipeline = YES;
    }

    if (scalePct >= 20 && scalePct <= 100) {
        CGFloat scale = (CGFloat)scalePct / 100.0f;
        if (fabs(scale - _currentScale) > 0.05f) {
            _currentScale = scale;
            restartPipeline = YES;
        }
    }

    if (bitrate >= 200000 && bitrate <= 10000000 && bitrate != _currentBitrate) {
        _currentBitrate = bitrate;
        if (_encoder) {
            _encoder.bitrate = _currentBitrate;
        }
    }

    if (restartPipeline) {
        [self _stopCaptureAndEncoderPipeline];
        [self _startCaptureAndEncoderPipeline];
    } else if (forceKey && _encoder) {
        [_encoder forceKeyframe];
    }
}

- (VSCRDeviceInfoPayload)tcpServerRequireDeviceInfo:(TcpServer *)server {
    uint32_t streamW = (uint32_t)_capture.displaySize.width;
    uint32_t streamH = (uint32_t)_capture.displaySize.height;
    return [DeviceInfoHelper getDeviceInfoWithStreamWidth:streamW streamHeight:streamH fps:(uint16_t)_currentFps];
}

- (void)tcpServerDidClientConnect:(TcpServer *)server clientCount:(NSInteger)count {
    NSLog(@"[iPhoneAgent] Client connected. Total active clients: %ld", (long)count);
    if (count > 0 && (!_capture || !_capture.isRunning)) {
        [self _startCaptureAndEncoderPipeline];
    }
    if (_encoder) {
        [_encoder forceKeyframe];
    }
}

- (void)tcpServerDidClientDisconnect:(TcpServer *)server clientCount:(NSInteger)count {
    NSLog(@"[iPhoneAgent] Client disconnected. Total active clients: %ld", (long)count);
    if (count == 0) {
        [self _stopCaptureAndEncoderPipeline];
    }
}

@end

%ctor {
    NSString *processName = [[NSProcessInfo processInfo] processName];
    if (![processName isEqualToString:@"SpringBoard"]) {
        return;
    }

    NSLog(@"==================================================");
    NSLog(@"[iPhoneAgent] Loaded into SpringBoard (Dopamine Rootless)");
    NSLog(@"==================================================");

    // Give SpringBoard 2 seconds to complete initial display compositor setup
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(2.0 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
        [[AgentController sharedController] start];
    });
}
