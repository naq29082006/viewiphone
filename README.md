# iPhone Multi Screen Manager (ViewiPhone)

Hệ thống xem và giám sát màn hình nhiều iPhone Jailbreak (Dopamine Rootless iOS 15.0 - 16.6.1) thời gian thực trên máy tính Windows thông qua mạng LAN/Wi-Fi hoặc USB.

---

## I. TỔNG QUAN KIẾN TRÚC

```
┌────────────────────────────────────────────────────────────────────────┐
│                        IPHONE (DOPAMINE ROOTLESS)                      │
│                                                                        │
│   SpringBoard (Injected via ElleKit)                                   │
│   ┌──────────────────┐    Zero-Copy      ┌──────────────────────┐      │
│   │  ScreenCapture   │ ────────────────> │     VideoEncoder     │      │
│   │ (CARenderServer/ │   CVPixelBuffer   │ (VTCompressionSession│      │
│   │    IOSurface)    │                   │      H.264 NALU)     │      │
│   └──────────────────┘                   └──────────────────────┘      │
│                                                      │ Annex-B NALUs   │
│                                                      ▼                 │
│                                          ┌──────────────────────┐      │
│                                          │  TcpServer (Port 5000│      │
│                                          │    Binary Protocol)  │      │
│                                          └──────────────────────┘      │
└──────────────────────────────────────────────────────┬─────────────────┘
                                                       │ TCP Socket
                                                       │ (LAN / Wi-Fi / USB)
                                                       ▼
┌────────────────────────────────────────────────────────────────────────┐
│                        WINDOWS CLIENT (.NET 8 WINFORMS)                │
│                                                                        │
│   ┌────────────────────────────────────────────────────────────────┐   │
│   │ DeviceManager (Quản lý 4 - 10 thiết bị đồng thời)              │   │
│   │ ┌────────────────────────────────────────────────────────────┐ │   │
│   │ │ Per-Device Session (Mỗi iPhone chạy luồng riêng biệt)      │ │   │
│   │ │                                                            │ │   │
│   │ │ TcpClientService ──> PacketParser ──> VideoDecoderService  │ │   │
│   │ │   (Handshake &          (Tách NALU        (FFmpeg P/Invoke  │ │   │
│   │ │    Heartbeat)           SPS/PPS/IDR)       libavcodec)     │ │   │
│   │ │                                                │           │ │   │
│   │ │                                                ▼           │ │   │
│   │ │                                        VideoViewer Control │ │   │
│   │ │                                         (Double-Buffered   │ │   │
│   │ │                                          GDI+ OSD Overlay) │ │   │
│   │ └────────────────────────────────────────────────────────────┘ │   │
│   └────────────────────────────────────────────────────────────────┘   │
│                                                                        │
│   Giao diện: MainForm (Sidebar, Header stats, Grid 1x1, 2x2, 3x3, 4x4) │
└────────────────────────────────────────────────────────────────────────┘
```

---

## II. CẤU TRÚC THƯ MỤC DỰ ÁN

```
Viewiphone/
│
├── iPhoneAgent/                    # Native iOS Tweak (Theos Rootless)
│   ├── Makefile                    # Config Dopamine Rootless
│   ├── control                     # Debian package metadata
│   ├── iPhoneAgent.plist           # SpringBoard filter bundle
│   ├── entitlements.plist          # IOSurface & private entitlements
│   ├── build_deb.sh                # Script build .deb
│   ├── README.md                   # Hướng dẫn build & deploy Agent
│   ├── Protocol/
│   │   └── VSCRProtocol.h          # Binary Protocol 28-byte header
│   ├── Capture/
│   │   ├── ScreenCapture.h
│   │   └── ScreenCapture.m         # CARenderServerRenderDisplay + IOSurface
│   ├── Encoder/
│   │   ├── VideoEncoder.h
│   │   └── VideoEncoder.m          # VTCompressionSession H.264
│   ├── Network/
│   │   ├── TcpServer.h
│   │   └── TcpServer.m             # Async socket listener port 5000
│   ├── DeviceInfo/
│   │   ├── DeviceInfoHelper.h
│   │   └── DeviceInfoHelper.m      # Lấy Model, iOS version, Resolution
│   ├── Security/
│   │   ├── AuthManager.h
│   │   └── AuthManager.m           # Xác thực Token bảo mật
│   └── Tweak.x                     # SpringBoard %ctor lifecycle hook
│
├── Viewiphone/                     # Windows C# .NET 8 WinForms Client
│   ├── Program.cs                  # Entry point, DPI, Global Exception Handler
│   ├── Viewiphone.csproj           # C# .NET 8 Project SDK
│   ├── Forms/
│   │   ├── MainForm.cs             # Giao diện chính (Sidebar, Header stats, Grid)
│   │   ├── AddDeviceForm.cs        # Form thêm/sửa thiết bị + Test Connection
│   │   └── SettingsForm.cs         # Form cấu hình FPS, Bitrate, Resolution
│   ├── Models/
│   │   ├── DeviceInfo.cs           # Tên máy, model, iOS version, độ phân giải
│   │   ├── DeviceConfig.cs         # Cấu hình IP, Port, Token lưu trữ
│   │   ├── StreamConfig.cs         # FPS, Bitrate, Scale, Hardware Accel
│   │   ├── VideoFrame.cs           # Frame NALU payload, PTS, Keyframe flag
│   │   └── ConnectionState.cs      # Trạng thái kết nối
│   ├── Services/
│   │   ├── DeviceManager.cs        # Quản lý 4 - 10 iPhone đồng thời
│   │   ├── TcpClientService.cs     # Socket TCP, Packet reassembly, Heartbeat
│   │   ├── VideoDecoderService.cs  # FFmpeg H.264 decoder + Swscale
│   │   ├── ConfigurationService.cs # Lưu/đọc JSON devices.json
│   │   ├── AuthenticationService.cs# Kiểm tra token
│   │   └── LoggingService.cs       # Serilog Console & Rolling File
│   ├── Controls/
│   │   ├── VideoViewer.cs          # Custom Control vẽ video, giữ aspect ratio
│   │   ├── DeviceCard.cs           # Card chứa Header + VideoViewer
│   │   └── DashboardControl.cs     # Grid Layout (Auto, 1, 2, 4, 6, 9, 16)
│   └── Protocol/
│       ├── PacketHeader.cs         # 28-byte Header binary struct
│       ├── PacketType.cs           # Enum PacketType & Flags
│       ├── PacketParser.cs         # Parse DeviceInfo, SPS/PPS, Frames
│       └── PacketBuilder.cs        # Build Handshake, Auth, Heartbeat, Config
│
├── download_ffmpeg.bat             # Tải FFmpeg DLLs tự động cho Windows
├── download_ffmpeg.ps1             # PowerShell script tải FFmpeg DLLs
└── README.md
```

---

## III. HƯỚNG DẪN CÀI ĐẶT & SỬ DỤNG

### 1. Triển khai iPhone Agent lên iPhone Dopamine
1. **Biên dịch package `.deb`:**
   ```bash
   cd iPhoneAgent
   export THEOS=~/theos
   export THEOS_PACKAGE_SCHEME=rootless
   make package FINALPACKAGE=1
   ```
   File `.deb` hoàn chỉnh nằm trong thư mục `iPhoneAgent/packages/`.
2. **Cài đặt lên iPhone:**
   - Chuyển file `.deb` sang iPhone (qua Filza, Sileo, hoặc SSH: `scp packages/*.deb mobile@<IP_IPHONE>:/var/mobile/`).
   - Mở Sileo / Filza và chọn **Install**, sau đó bấm **Respring**.
3. **Hoạt động ngầm:**
   - Agent tự động khởi động trong `SpringBoard` và mở cổng TCP `5000`.
   - Token xác thực mặc định: `viewiphone_secret`.

### 2. Chạy Windows Client
1. **Tải FFmpeg Native Binaries (chỉ cần chạy 1 lần):**
   - Chạy file `download_ffmpeg.bat` trong thư mục gốc.
   - Script sẽ tự động tải các file `avcodec-*.dll`, `avutil-*.dll`, `swscale-*.dll` và đặt vào thư mục `bin/Debug/net8.0-windows7.0/ffmpeg/`.
2. **Biên dịch và chạy ứng dụng:**
   ```powershell
   dotnet build Viewiphone/Viewiphone.csproj
   dotnet run --project Viewiphone/Viewiphone.csproj
   ```
3. **Thao tác trên giao diện Windows:**
   - Bấm **➕ Add Device**.
   - Nhập Tên máy, IP của iPhone (ví dụ: `192.168.1.100`), Port (`5000`), Token (`viewiphone_secret`).
   - Bấm **🔍 Test Connection** để kiểm tra bắt tay và đọc thông tin máy từ iPhone.
   - Bấm **Connect Now** hoặc **Save Device**.
   - Màn hình iPhone sẽ hiển thị trực tiếp với độ trễ thấp và FPS thời gian thực.
   - Chọn Grid layout (Auto, 1x1, 2x2, 3x3, 4x4) để xem đồng thời 4 đến 10 iPhone.
   - Nhấn đúp chuột vào bất kỳ màn hình nào để phóng to toàn màn hình, nhấn đúp lần nữa để quay lại Grid.
