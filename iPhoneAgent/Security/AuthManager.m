#import "AuthManager.h"

@implementation AuthManager

+ (instancetype)sharedManager {
    static AuthManager *instance = nil;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        instance = [[AuthManager alloc] init];
    });
    return instance;
}

- (NSString *)configuredToken {
    NSDictionary *dict = [NSDictionary dictionaryWithContentsOfFile:AGENT_PREFS_PATH];
    NSString *token = dict[@"AuthToken"];
    if (token && [token length] > 0) {
        return token;
    }
    return DEFAULT_AUTH_TOKEN;
}

- (BOOL)validateToken:(NSString *)token {
    if (!token) return NO;
    NSString *expected = [self configuredToken];
    // Trim whitespace and compare
    NSString *trimmed = [token stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceAndNewlineCharacterSet]];
    return [trimmed isEqualToString:expected];
}

- (void)saveToken:(NSString *)token {
    NSMutableDictionary *dict = [NSMutableDictionary dictionaryWithContentsOfFile:AGENT_PREFS_PATH] ?: [NSMutableDictionary dictionary];
    dict[@"AuthToken"] = token;
    [dict writeToFile:AGENT_PREFS_PATH atomically:YES];
}

@end
