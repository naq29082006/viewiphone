#import <Foundation/Foundation.h>
#import <CoreVideo/CoreVideo.h>
#import <IOSurface/IOSurfaceRef.h>

@class ScreenCapture;

@protocol ScreenCaptureDelegate <NSObject>
- (void)screenCapture:(ScreenCapture *)capture didCaptureBuffer:(CVPixelBufferRef)pixelBuffer pts:(uint64_t)ptsUs;
@end

@interface ScreenCapture : NSObject

@property (nonatomic, weak) id<ScreenCaptureDelegate> delegate;
@property (nonatomic, assign) NSInteger fps;
@property (nonatomic, assign) CGFloat captureScale; // 0.25 to 1.0 (default 0.5)
@property (nonatomic, readonly) CGSize displaySize;
@property (nonatomic, readonly) NSString *lastError;
@property (nonatomic, readonly) BOOL isRunning;

- (instancetype)initWithFps:(NSInteger)fps scale:(CGFloat)scale;
- (BOOL)start;
- (void)stop;

@end
