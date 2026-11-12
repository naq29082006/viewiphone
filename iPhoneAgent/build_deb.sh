#!/bin/bash
# ==============================================================================
# Script biên dịch iPhoneAgent cho Dopamine Rootless Jailbreak (iOS 15.0 - 16.6.1)
# ==============================================================================

set -e

if [ -z "$THEOS" ]; then
    echo "[!] Biến môi trường THEOS chưa được thiết lập."
    echo "[*] Vui lòng cài đặt Theos (https://theos.dev) hoặc gán: export THEOS=~/theos"
    exit 1
fi

echo "[*] Đang biên dịch iPhoneAgent (Dopamine Rootless)..."
export THEOS_PACKAGE_SCHEME=rootless
make clean
make package FINALPACKAGE=1

echo "[+] Biên dịch thành công! File .deb nằm trong thư mục packages/"
ls -la packages/*.deb
