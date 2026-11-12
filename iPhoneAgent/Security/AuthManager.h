#import <Foundation/Foundation.h>

#define AGENT_PREFS_PATH @"/var/mobile/Library/Preferences/com.viewiphone.agent.plist"
#define DEFAULT_AUTH_TOKEN @"viewiphone_secret"

@interface AuthManager : NSObject

+ (instancetype)sharedManager;
- (BOOL)validateToken:(NSString *)token;
- (NSString *)configuredToken;
- (void)saveToken:(NSString *)token;

@end
