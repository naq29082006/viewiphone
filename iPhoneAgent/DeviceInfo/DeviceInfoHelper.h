#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import "../Protocol/VSCRProtocol.h"

@interface DeviceInfoHelper : NSObject

+ (VSCRDeviceInfoPayload)getDeviceInfoWithStreamWidth:(uint32_t)streamWidth 
                                         streamHeight:(uint32_t)streamHeight 
                                                  fps:(uint16_t)fps;

+ (NSString *)deviceModelIdentifier;
+ (NSString *)deviceName;
+ (NSString *)osVersion;
+ (CGSize)nativePixelSize;

@end
