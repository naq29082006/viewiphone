#import "DeviceInfoHelper.h"
#import <sys/utsname.h>

@implementation DeviceInfoHelper

+ (NSString *)deviceModelIdentifier {
    struct utsname systemInfo;
    uname(&systemInfo);
    return [NSString stringWithCString:systemInfo.machine encoding:NSUTF8StringEncoding];
}

+ (NSString *)deviceName {
    return [[UIDevice currentDevice] name] ?: @"iPhone";
}

+ (NSString *)osVersion {
    return [[UIDevice currentDevice] systemVersion] ?: @"15.0";
}

+ (CGSize)nativePixelSize {
    UIScreen *mainScreen = [UIScreen mainScreen];
    CGFloat scale = mainScreen.scale;
    CGSize bounds = mainScreen.bounds.size;
    return CGSizeMake(round(bounds.width * scale), round(bounds.height * scale));
}

+ (VSCRDeviceInfoPayload)getDeviceInfoWithStreamWidth:(uint32_t)streamWidth 
                                         streamHeight:(uint32_t)streamHeight 
                                                  fps:(uint16_t)fps {
    VSCRDeviceInfoPayload payload;
    memset(&payload, 0, sizeof(payload));

    NSString *name = [self deviceName];
    NSString *model = [self deviceModelIdentifier];
    NSString *osVer = [self osVersion];
    CGSize nativeSize = [self nativePixelSize];

    strncpy(payload.device_name, [name UTF8String], sizeof(payload.device_name) - 1);
    strncpy(payload.model, [model UTF8String], sizeof(payload.model) - 1);
    strncpy(payload.ios_version, [osVer UTF8String], sizeof(payload.ios_version) - 1);

    payload.native_width = (uint32_t)nativeSize.width;
    payload.native_height = (uint32_t)nativeSize.height;
    payload.stream_width = streamWidth;
    payload.stream_height = streamHeight;
    payload.fps = fps;

    return payload;
}

@end
