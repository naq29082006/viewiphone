#import <Foundation/Foundation.h>
#import <CoreVideo/CoreVideo.h>
#import <VideoToolbox/VideoToolbox.h>

@class VideoEncoder;

@protocol VideoEncoderDelegate <NSObject>
- (void)videoEncoder:(VideoEncoder *)encoder didProduceSPS:(NSData *)sps pps:(NSData *)pps;
- (void)videoEncoder:(VideoEncoder *)encoder didProduceAnnexBFrame:(NSData *)frameData isKeyframe:(BOOL)isKeyframe pts:(uint64_t)ptsUs;
@end

@interface VideoEncoder : NSObject

@property (nonatomic, weak) id<VideoEncoderDelegate> delegate;
@property (nonatomic, assign) NSInteger bitrate;           // in bits/sec (e.g. 1500000)
@property (nonatomic, assign) NSInteger keyframeInterval;  // in frames
@property (nonatomic, assign) float quality;               // 0.0 - 1.0 (default 0.6)
@property (nonatomic, readonly) BOOL isRunning;
@property (nonatomic, readonly) int width;
@property (nonatomic, readonly) int height;

- (BOOL)startWithWidth:(int)width height:(int)height fps:(int)fps;
- (void)stop;
- (void)encodePixelBuffer:(CVPixelBufferRef)pixelBuffer pts:(uint64_t)ptsUs;
- (void)forceKeyframe;

@end
