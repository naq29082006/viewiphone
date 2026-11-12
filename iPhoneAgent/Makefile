export THEOS_PACKAGE_SCHEME = rootless
TARGET := iphone:clang:latest:15.0
ARCHS := arm64 arm64e

DEBUG = 0
FINALPACKAGE = 1

include $(THEOS)/makefiles/common.mk

TWEAK_NAME = iPhoneAgent

iPhoneAgent_FILES = Tweak.x \
                    Capture/ScreenCapture.m \
                    Encoder/VideoEncoder.m \
                    Network/TcpServer.m \
                    DeviceInfo/DeviceInfoHelper.m \
                    Security/AuthManager.m

iPhoneAgent_CFLAGS = -fobjc-arc -I. -IProtocol -O3
iPhoneAgent_FRAMEWORKS = UIKit CoreGraphics CoreVideo QuartzCore VideoToolbox Foundation
iPhoneAgent_PRIVATE_FRAMEWORKS = IOSurface

include $(THEOS_MAKE_PATH)/tweak.mk

after-install::
	install.exec "killall -9 SpringBoard"
