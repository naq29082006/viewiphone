# iPhone Screen Agent (Dopamine Rootless)

Tweak native chạy trên tiến trình `SpringBoard` của iPhone Jailbreak Dopamine (iOS 15.0 - 16.6.1).
Lấy hình ảnh trực tiếp từ GPU Compositor qua `CARenderServerRenderDisplay` + `IOSurface`, mã hóa phần cứng H.264 qua `VideoToolbox` và mở TCP Server trên cổng `5000`.

## 1. Yêu cầu môi trường biên dịch
- macOS / Linux / WSL hoặc biên dịch trực tiếp trên iPhone qua NewTerm (đã cài Theos).
- Theos: `git clone --recursive https://github.com/theos/theos.git ~/theos`
- iOS SDK 15.0+ hoặc 16.5 được đặt trong `$THEOS/sdks/`.

## 2. Cách biên dịch file .deb
```bash
export THEOS=~/theos
export THEOS_PACKAGE_SCHEME=rootless
cd iPhoneAgent
make package FINALPACKAGE=1
```
File `.deb` hoàn chỉnh sẽ nằm trong thư mục `iPhoneAgent/packages/com.viewiphone.agent_1.0.0_iphoneos-arm64.deb`.

## 3. Cài đặt lên iPhone Dopamine
### Cách 1: Qua Sileo / Filza
1. Gửi file `.deb` sang iPhone (qua AirDrop, SSH SCP hoặc Web server local).
2. Mở file trong Filza / Sileo và chọn **Install**.
3. Bấm **Respring**.

### Cách 2: Qua SSH Terminal
```bash
scp packages/*.deb mobile@<IP_IPHONE>:/var/mobile/
ssh mobile@<IP_IPHONE>
sudo dpkg -i /var/mobile/*.deb
killall -9 SpringBoard
```

## 4. Kiểm tra dịch vụ
Sau khi Respring, Agent tự động chạy ngầm trong SpringBoard và lắng nghe cổng 5000:
```bash
# Kiểm tra port 5000 đã mở
netstat -an | grep 5000
# Hoặc từ máy tính Windows:
Test-NetConnection -ComputerName <IP_IPHONE> -Port 5000
```
- Token mặc định: `viewiphone_secret`
- Cấu hình file token tùy biến (nếu muốn đổi): `/var/mobile/Library/Preferences/com.viewiphone.agent.plist`
