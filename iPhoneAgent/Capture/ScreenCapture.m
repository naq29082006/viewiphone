#import "ScreenCapture.h"
#import <UIKit/UIKit.h>
#import <dlfcn.h>
#import <mach/mach_port.h>

// Private CARenderServer signature
typedef kern_return_t (*CARenderDisplayFn)(mach_port_t, CFStringRef, IOSurfaceRef, int, int);
typedef CFTypeRef (*UICreateScreenUIImageFn)(void);

typedef enum {
    BackendNone,
    BackendCARenderServer,
    BackendUIPrivate
} CaptureBackend;

@implementation ScreenCapture {
    dispatch_source_t _timer;
    dispatch_queue_t _queue;
    IOSurfaceRef _surface;
    CVPixelBufferRef _wrappedPB;
    CFAbsoluteTime _startTime;
    CARenderDisplayFn _fnRenderDisplay;
    UICreateScreenUIImageFn _fnUICreateScreen;
    CVPixelBufferPoolRef _fallbackPool;
    BOOL _busy;
    CaptureBackend _backend;
    NSUInteger _consecutiveBlack;

    // Static frame detection: 16x16 samples of B channel
    uint8_t _lastSamples[256];
    BOOL _haveLastSamples;
    NSUInteger _staticStreak;
    BOOL _isIdle;
    NSInteger _activeFps;
    NSInteger _idleFps;
}

- (instancetype)initWithFps:(NSInteger)fps scale:(CGFloat)scale {
    if ((self = [super init])) {
        _fps = (fps > 0) ? fps : 15;
        _activeFps = _fps;
        _idleFps = 4;
        _captureScale = (scale > 0.1f && scale <= 1.0f) ? scale : 0.5f;

        UIScreen *screen = [UIScreen mainScreen];
        CGFloat displayScale = screen.scale;
        CGSize bounds = screen.bounds.size;

        CGFloat factor = _captureScale * displayScale;
        // Ensure dimensions are even (required for H.264 macroblocks)
        size_t w = ((size_t)round(bounds.width * factor)) & ~1;
        size_t h = ((size_t)round(bounds.height * factor)) & ~1;
        _displaySize = CGSizeMake((CGFloat)w, (CGFloat)h);

        _queue = dispatch_queue_create("com.viewiphone.capture", DISPATCH_QUEUE_SERIAL);
        [self _resolveSymbols];
    }
    return self;
}

- (instancetype)init {
    return [self initWithFps:15 scale:0.5f];
}

- (void)_resolveSymbols {
    void *qc = dlopen("/System/Library/Frameworks/QuartzCore.framework/QuartzCore", RTLD_NOW);
    if (qc) {
        _fnRenderDisplay = (CARenderDisplayFn)dlsym(qc, "CARenderServerRenderDisplay");
    }
    _fnUICreateScreen = (UICreateScreenUIImageFn)dlsym(RTLD_DEFAULT, "_UICreateScreenUIImage");
}

- (void)dealloc {
    [self stop];
    if (_wrappedPB) {
        CVPixelBufferRelease(_wrappedPB);
        _wrappedPB = NULL;
    }
    if (_surface) {
        CFRelease(_surface);
        _surface = NULL;
    }
    if (_fallbackPool) {
        CVPixelBufferPoolRelease(_fallbackPool);
        _fallbackPool = NULL;
    }
}

- (BOOL)_allocateSurfaceAndBuffer {
    if (_surface && _wrappedPB) return YES;

    size_t w = (size_t)_displaySize.width;
    size_t h = (size_t)_displaySize.height;
    size_t bytesPerElement = 4;
    // Align row bytes to 64 for optimal GPU access
    size_t bytesPerRow = ((w * bytesPerElement) + 63) & ~((size_t)63);

    NSDictionary *props = @{
        (id)kIOSurfaceWidth: @(w),
        (id)kIOSurfaceHeight: @(h),
        (id)kIOSurfacePixelFormat: @((unsigned int)kCVPixelFormatType_32BGRA),
        (id)kIOSurfaceBytesPerElement: @(bytesPerElement),
        (id)kIOSurfaceBytesPerRow: @(bytesPerRow),
        (id)kIOSurfaceAllocSize: @(bytesPerRow * h),
    };

    _surface = IOSurfaceCreate((__bridge CFDictionaryRef)props);
    if (!_surface) {
        _lastError = @"Failed to create IOSurface";
        return NO;
    }

    NSDictionary *attrs = @{ (id)kCVPixelBufferIOSurfacePropertiesKey: @{} };
    CVReturn ret = CVPixelBufferCreateWithIOSurface(
        kCFAllocatorDefault,
        _surface,
        (__bridge CFDictionaryRef)attrs,
        &_wrappedPB
    );

    if (ret != kCVReturnSuccess || !_wrappedPB) {
        _lastError = [NSString stringWithFormat:@"CVPixelBufferCreateWithIOSurface failed (code: %d)", ret];
        if (_surface) {
            CFRelease(_surface);
            _surface = NULL;
        }
        return NO;
    }

    return YES;
}

- (BOOL)start {
    if (_timer) return YES;

    if (!_fnRenderDisplay && !_fnUICreateScreen) {
        _lastError = @"No capture backend symbols resolved in SpringBoard";
        return NO;
    }

    if (_fnRenderDisplay && [self _allocateSurfaceAndBuffer]) {
        _backend = BackendCARenderServer;
    } else if (_fnUICreateScreen) {
        _backend = BackendUIPrivate;
    } else {
        _lastError = @"Failed to initialize capture backend surface";
        return NO;
    }

    _startTime = CFAbsoluteTimeGetCurrent();
    _timer = dispatch_source_create(DISPATCH_SOURCE_TYPE_TIMER, 0, 0, _queue);
    
    __weak ScreenCapture *weakSelf = self;
    dispatch_source_set_event_handler(_timer, ^{
        [weakSelf _captureTick];
    });

    [self _applyTimerInterval];
    dispatch_resume(_timer);
    _isRunning = YES;
    return YES;
}

- (void)stop {
    _isRunning = NO;
    if (_timer) {
        dispatch_source_cancel(_timer);
        _timer = NULL;
    }
}

- (void)_applyTimerInterval {
    if (!_timer) return;
    NSInteger currentFps = _isIdle ? _idleFps : _activeFps;
    if (currentFps < 1) currentFps = 1;
    uint64_t intervalNs = (uint64_t)((1.0 / (double)currentFps) * NSEC_PER_SEC);
    dispatch_source_set_timer(_timer, DISPATCH_TIME_NOW, intervalNs, intervalNs / 4);
}

static BOOL is_surface_black(IOSurfaceRef surface) {
    void *base = IOSurfaceGetBaseAddress(surface);
    if (!base) return YES;
    size_t bpr = IOSurfaceGetBytesPerRow(surface);
    size_t w = IOSurfaceGetWidth(surface);
    size_t h = IOSurfaceGetHeight(surface);
    if (w < 8 || h < 8) return NO;

    const uint8_t *midRow = (const uint8_t *)base + (h / 2) * bpr;
    uint32_t accumulator = 0;
    for (int i = 0; i < 16; i++) {
        const uint8_t *pixel = midRow + (w / 16) * i * 4;
        accumulator |= pixel[0] | pixel[1] | pixel[2];
    }
    return accumulator == 0;
}

static BOOL sample_bytes_unchanged(const void *base, size_t bpr, size_t w, size_t h,
                                   uint8_t lastSamples[256], BOOL *haveLast) {
    if (!base || w < 32 || h < 32) return NO;
    uint8_t current[256];
    for (int row = 0; row < 16; row++) {
        size_t y = (size_t)row * h / 16 + h / 32;
        const uint8_t *rowPtr = (const uint8_t *)base + y * bpr;
        for (int col = 0; col < 16; col++) {
            size_t x = (size_t)col * w / 16 + w / 32;
            current[row * 16 + col] = rowPtr[x * 4]; // Blue channel sample
        }
    }
    BOOL same = *haveLast && (memcmp(current, lastSamples, 256) == 0);
    memcpy(lastSamples, current, 256);
    *haveLast = YES;
    return same;
}

- (BOOL)_evaluateStaticFrame:(BOOL)unchanged {
    if (unchanged) {
        _staticStreak++;
        if (!_isIdle && _staticStreak > (NSUInteger)(_activeFps * 2)) {
            _isIdle = YES;
            [self _applyTimerInterval];
        }
        return NO; // Skip encoding static frame
    }
    if (_isIdle) {
        _isIdle = NO;
        [self _applyTimerInterval];
    }
    _staticStreak = 0;
    return YES; // Emit frame
}

- (void)_captureTick {
    if (_busy) return;
    _busy = YES;

    if (_backend == BackendCARenderServer) {
        kern_return_t kr = _fnRenderDisplay(0, NULL, _surface, 0, 0);
        if (kr == KERN_SUCCESS) {
            IOSurfaceLock(_surface, kIOSurfaceLockReadOnly, NULL);
            BOOL black = (_consecutiveBlack < 30) && is_surface_black(_surface);
            BOOL unchanged = !black && sample_bytes_unchanged(
                IOSurfaceGetBaseAddress(_surface),
                IOSurfaceGetBytesPerRow(_surface),
                IOSurfaceGetWidth(_surface),
                IOSurfaceGetHeight(_surface),
                _lastSamples, &_haveLastSamples
            );
            IOSurfaceUnlock(_surface, kIOSurfaceLockReadOnly, NULL);

            if (black) {
                if (++_consecutiveBlack >= 30 && _fnUICreateScreen) {
                    _backend = BackendUIPrivate;
                    _busy = NO;
                    [self _captureTick];
                    return;
                }
                _busy = NO;
                return;
            }
            _consecutiveBlack = 0;

            if (![self _evaluateStaticFrame:unchanged]) {
                _busy = NO;
                return;
            }

            uint64_t ptsUs = (uint64_t)((CFAbsoluteTimeGetCurrent() - _startTime) * 1e6);
            if (self.delegate && _wrappedPB) {
                [self.delegate screenCapture:self didCaptureBuffer:_wrappedPB pts:ptsUs];
            }
        } else {
            _lastError = [NSString stringWithFormat:@"CARenderServerRenderDisplay kr=%d", kr];
        }
    } else if (_backend == BackendUIPrivate) {
        UIImage *screenImg = (__bridge_transfer UIImage *)_fnUICreateScreen();
        if (screenImg && screenImg.CGImage) {
            CGImageRef cgImg = screenImg.CGImage;
            size_t w = CGImageGetWidth(cgImg);
            size_t h = CGImageGetHeight(cgImg);

            if (!_fallbackPool) {
                NSDictionary *poolAttrs = @{
                    (id)kCVPixelBufferPixelFormatTypeKey: @(kCVPixelFormatType_32BGRA),
                    (id)kCVPixelBufferWidthKey: @(w),
                    (id)kCVPixelBufferHeightKey: @(h),
                };
                CVPixelBufferPoolCreate(kCFAllocatorDefault, NULL, (__bridge CFDictionaryRef)poolAttrs, &_fallbackPool);
            }

            CVPixelBufferRef pixelBuf = NULL;
            if (_fallbackPool) {
                CVPixelBufferPoolCreatePixelBuffer(kCFAllocatorDefault, _fallbackPool, &pixelBuf);
            }

            if (pixelBuf) {
                CVPixelBufferLockBaseAddress(pixelBuf, 0);
                CGColorSpaceRef colorSpace = CGColorSpaceCreateDeviceRGB();
                CGContextRef context = CGBitmapContextCreate(
                    CVPixelBufferGetBaseAddress(pixelBuf),
                    w, h, 8,
                    CVPixelBufferGetBytesPerRow(pixelBuf),
                    colorSpace,
                    kCGImageAlphaNoneSkipFirst | kCGBitmapByteOrder32Little
                );
                CGColorSpaceRelease(colorSpace);

                if (context) {
                    CGContextDrawImage(context, CGRectMake(0, 0, w, h), cgImg);
                    CGContextRelease(context);
                }

                BOOL unchanged = sample_bytes_unchanged(
                    CVPixelBufferGetBaseAddress(pixelBuf),
                    CVPixelBufferGetBytesPerRow(pixelBuf),
                    w, h,
                    _lastSamples, &_haveLastSamples
                );
                CVPixelBufferUnlockBaseAddress(pixelBuf, 0);

                if ([self _evaluateStaticFrame:unchanged]) {
                    uint64_t ptsUs = (uint64_t)((CFAbsoluteTimeGetCurrent() - _startTime) * 1e6);
                    if (self.delegate) {
                        [self.delegate screenCapture:self didCaptureBuffer:pixelBuf pts:ptsUs];
                    }
                }
                CVPixelBufferRelease(pixelBuf);
            }
        }
    }

    _busy = NO;
}

@end
