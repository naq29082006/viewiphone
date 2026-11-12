#import "VideoEncoder.h"

@implementation VideoEncoder {
    VTCompressionSessionRef _session;
    BOOL _forceKey;
    int _currentFps;
}

- (instancetype)init {
    if ((self = [super init])) {
        _bitrate = 1500 * 1000; // 1.5 Mbps
        _keyframeInterval = 120; // Keyframe every 120 frames
        _quality = 0.6f;
    }
    return self;
}

- (void)dealloc {
    [self stop];
}

static void CompressionCallback(void *outputCallbackRefCon,
                                void *sourceFrameRefCon,
                                OSStatus status,
                                VTEncodeInfoFlags infoFlags,
                                CMSampleBufferRef sampleBuffer)
{
    VideoEncoder *encoder = (__bridge VideoEncoder *)outputCallbackRefCon;
    if (status != noErr || !sampleBuffer || !CMSampleBufferDataIsReady(sampleBuffer)) {
        return;
    }
    [encoder _processSampleBuffer:sampleBuffer];
}

- (BOOL)startWithWidth:(int)width height:(int)height fps:(int)fps {
    [self stop];
    _width = width;
    _height = height;
    _currentFps = (fps > 0) ? fps : 15;

    NSDictionary *sourceImageAttrs = @{
        (id)kCVPixelBufferPixelFormatTypeKey: @(kCVPixelFormatType_32BGRA),
        (id)kCVPixelBufferIOSurfacePropertiesKey: @{},
    };

    OSStatus status = VTCompressionSessionCreate(
        kCFAllocatorDefault,
        _width,
        _height,
        kCMVideoCodecType_H264,
        NULL,
        (__bridge CFDictionaryRef)sourceImageAttrs,
        NULL,
        CompressionCallback,
        (__bridge void *)self,
        &_session
    );

    if (status != noErr || !_session) {
        NSLog(@"[VideoEncoder] VTCompressionSessionCreate failed with status %d", (int)status);
        return NO;
    }

    // Configure ultra-low latency streaming settings
    VTSessionSetProperty(_session, kVTCompressionPropertyKey_RealTime, kCFBooleanTrue);
    VTSessionSetProperty(_session, kVTCompressionPropertyKey_AllowFrameReordering, kCFBooleanFalse);
    VTSessionSetProperty(_session, kVTCompressionPropertyKey_ProfileLevel, kVTProfileLevel_H264_Baseline_AutoLevel);

    int maxDelay = 0;
    CFNumberRef delayNum = CFNumberCreate(NULL, kCFNumberIntType, &maxDelay);
    VTSessionSetProperty(_session, kVTCompressionPropertyKey_MaxFrameDelayCount, delayNum);
    CFRelease(delayNum);

    int br = (int)_bitrate;
    CFNumberRef bitrateNum = CFNumberCreate(NULL, kCFNumberIntType, &br);
    VTSessionSetProperty(_session, kVTCompressionPropertyKey_AverageBitRate, bitrateNum);
    CFRelease(bitrateNum);

    int kfi = (int)_keyframeInterval;
    CFNumberRef kfiNum = CFNumberCreate(NULL, kCFNumberIntType, &kfi);
    VTSessionSetProperty(_session, kVTCompressionPropertyKey_MaxKeyFrameInterval, kfiNum);
    CFRelease(kfiNum);

    int expectedFps = _currentFps;
    CFNumberRef fpsNum = CFNumberCreate(NULL, kCFNumberIntType, &expectedFps);
    VTSessionSetProperty(_session, kVTCompressionPropertyKey_ExpectedFrameRate, fpsNum);
    CFRelease(fpsNum);

    float q = _quality;
    CFNumberRef qualityNum = CFNumberCreate(NULL, kCFNumberFloatType, &q);
    VTSessionSetProperty(_session, kVTCompressionPropertyKey_Quality, qualityNum);
    CFRelease(qualityNum);

    VTCompressionSessionPrepareToEncodeFrames(_session);
    _isRunning = YES;
    _forceKey = YES; // Force keyframe on first encode
    NSLog(@"[VideoEncoder] Session started %dx%d @ %d fps, bitrate: %ld bps", _width, _height, _currentFps, (long)_bitrate);
    return YES;
}

- (void)stop {
    _isRunning = NO;
    if (_session) {
        VTCompressionSessionInvalidate(_session);
        CFRelease(_session);
        _session = NULL;
    }
}

- (void)forceKeyframe {
    _forceKey = YES;
}

- (void)encodePixelBuffer:(CVPixelBufferRef)pixelBuffer pts:(uint64_t)ptsUs {
    if (!_session || !pixelBuffer) return;

    CMTime presentationTimeStamp = CMTimeMake((int64_t)ptsUs, 1000000);
    CMTime duration = CMTimeMake(1, _currentFps > 0 ? _currentFps : 15);

    NSDictionary *frameProps = nil;
    if (_forceKey) {
        frameProps = @{ (id)kVTEncodeFrameOptionKey_ForceKeyFrame: @YES };
        _forceKey = NO;
    }

    OSStatus status = VTCompressionSessionEncodeFrame(
        _session,
        pixelBuffer,
        presentationTimeStamp,
        duration,
        (__bridge CFDictionaryRef)frameProps,
        NULL,
        NULL
    );

    if (status != noErr) {
        NSLog(@"[VideoEncoder] Encode frame error: %d", (int)status);
    }
}

- (void)_processSampleBuffer:(CMSampleBufferRef)sampleBuffer {
    BOOL isKeyframe = NO;
    CFArrayRef attachments = CMSampleBufferGetSampleAttachmentsArray(sampleBuffer, false);
    if (attachments && CFArrayGetCount(attachments) > 0) {
        CFDictionaryRef dict = CFArrayGetValueAtIndex(attachments, 0);
        isKeyframe = !CFDictionaryContainsKey(dict, kCMSampleAttachmentKey_NotSync);
    }

    // If keyframe, extract SPS and PPS
    if (isKeyframe) {
        CMFormatDescriptionRef formatDesc = CMSampleBufferGetFormatDescription(sampleBuffer);
        if (formatDesc) {
            size_t spsSize = 0, ppsSize = 0;
            const uint8_t *spsPtr = NULL;
            const uint8_t *ppsPtr = NULL;

            OSStatus s0 = CMVideoFormatDescriptionGetH264ParameterSetAtIndex(formatDesc, 0, &spsPtr, &spsSize, NULL, NULL);
            OSStatus s1 = CMVideoFormatDescriptionGetH264ParameterSetAtIndex(formatDesc, 1, &ppsPtr, &ppsSize, NULL, NULL);

            if (s0 == noErr && s1 == noErr && spsPtr && ppsPtr) {
                NSData *spsData = [NSData dataWithBytes:spsPtr length:spsSize];
                NSData *ppsData = [NSData dataWithBytes:ppsPtr length:ppsSize];
                if (self.delegate) {
                    [self.delegate videoEncoder:self didProduceSPS:spsData pps:ppsData];
                }
            }
        }
    }

    // Convert AVCC to Annex-B (replace 4-byte length prefix with 0x00000001)
    CMBlockBufferRef blockBuffer = CMSampleBufferGetDataBuffer(sampleBuffer);
    if (!blockBuffer) return;

    size_t totalLength = 0;
    char *dataPointer = NULL;
    OSStatus status = CMBlockBufferGetDataPointer(blockBuffer, 0, NULL, &totalLength, &dataPointer);
    if (status != noErr || !dataPointer || totalLength < 4) return;

    NSMutableData *annexBData = [NSMutableData dataWithCapacity:totalLength + 64];
    static const uint8_t startCode[4] = { 0x00, 0x00, 0x00, 0x01 };

    size_t offset = 0;
    while (offset < totalLength - 4) {
        uint32_t naluLength = 0;
        memcpy(&naluLength, dataPointer + offset, 4);
        naluLength = CFSwapInt32BigToHost(naluLength);

        if (naluLength == 0 || offset + 4 + naluLength > totalLength) {
            break;
        }

        [annexBData appendBytes:startCode length:4];
        [annexBData appendBytes:dataPointer + offset + 4 length:naluLength];
        offset += 4 + naluLength;
    }

    CMTime pts = CMSampleBufferGetPresentationTimeStamp(sampleBuffer);
    uint64_t ptsUs = (uint64_t)(CMTimeGetSeconds(pts) * 1000000ULL);

    if (self.delegate && annexBData.length > 0) {
        [self.delegate videoEncoder:self didProduceAnnexBFrame:annexBData isKeyframe:isKeyframe pts:ptsUs];
    }
}

@end
