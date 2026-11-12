// ==============================================================================
// VSCRProtocol.h - ViewScreen Binary Streaming Protocol Definition
// ==============================================================================
#ifndef VSCR_PROTOCOL_H
#define VSCR_PROTOCOL_H

#include <stdint.h>

#define VSCR_MAGIC           0x56534352  // ASCII "VSCR" (ViewScreen)
#define VSCR_VERSION         0x0001
#define VSCR_DEFAULT_PORT    5000

// Packet Types
typedef enum : uint16_t {
    VSCR_PKT_HANDSHAKE      = 0x0001, // Client -> Server (init session)
    VSCR_PKT_AUTH           = 0x0002, // Client -> Server (token validation)
    VSCR_PKT_DEVICE_INFO    = 0x0003, // Server -> Client (device metadata)
    VSCR_PKT_VIDEO_CONFIG   = 0x0004, // Server -> Client (H.264 SPS & PPS)
    VSCR_PKT_VIDEO_FRAME    = 0x0005, // Server -> Client (H.264 Annex-B NALUs)
    VSCR_PKT_HEARTBEAT      = 0x0006, // Bidirectional (Keep-alive Ping/Pong)
    VSCR_PKT_ERROR          = 0x0007, // Server -> Client (Error code + message)
    VSCR_PKT_DISCONNECT     = 0x0008, // Bidirectional (Clean disconnect)
    VSCR_PKT_CLIENT_CONFIG  = 0x0009  // Client -> Server (Set FPS, Bitrate, Scale, Request Keyframe)
} VSCRPacketType;

// Packet Flags
typedef enum : uint16_t {
    VSCR_FLAG_NONE          = 0x0000,
    VSCR_FLAG_KEYFRAME      = 0x0001, // Frame is an IDR Keyframe
    VSCR_FLAG_AUTH_SUCCESS  = 0x0002,
    VSCR_FLAG_AUTH_FAILED   = 0x0004,
    VSCR_FLAG_PING          = 0x0008,
    VSCR_FLAG_PONG          = 0x0010
} VSCRPacketFlags;

#pragma pack(push, 1)

// Fixed 28-byte packet header
typedef struct {
    uint32_t magic;         // 0x56534352 ("VSCR")
    uint16_t version;       // Protocol version (1)
    uint16_t packet_type;   // VSCRPacketType
    uint16_t flags;         // VSCRPacketFlags (e.g. VSCR_FLAG_KEYFRAME)
    uint16_t reserved;      // Reserved for alignment / future use (0)
    uint32_t frame_id;      // Monotonically increasing sequence number
    uint64_t timestamp_us;  // Presentation timestamp (microseconds)
    uint32_t payload_len;   // Byte length of payload following this header
} VSCRHeader;

// Payload for VSCR_PKT_DEVICE_INFO
typedef struct {
    char     device_name[64];    // e.g. "iPhone 11 Pro"
    char     model[32];          // e.g. "iPhone12,3"
    char     ios_version[32];    // e.g. "15.4.1"
    uint32_t native_width;       // Screen native width (pixels)
    uint32_t native_height;      // Screen native height (pixels)
    uint32_t stream_width;       // Current encoded width
    uint32_t stream_height;      // Current encoded height
    uint16_t fps;                // Configured FPS
    uint16_t reserved;
} VSCRDeviceInfoPayload;

// Payload for VSCR_PKT_CLIENT_CONFIG
typedef struct {
    uint16_t target_fps;         // Desired FPS (10, 15, 20, 30)
    uint16_t target_scale_pct;   // Scale percentage (e.g. 50 = 50%, 100 = 100%)
    uint32_t target_bitrate;     // Desired bitrate in bps (e.g. 1500000)
    uint8_t  request_keyframe;   // 1 = force generate IDR keyframe immediately
    uint8_t  reserved[3];
} VSCRClientConfigPayload;

// Payload for VSCR_PKT_ERROR
typedef struct {
    uint32_t error_code;
    char     message[128];
} VSCRErrorPayload;

#pragma pack(pop)

#endif // VSCR_PROTOCOL_H
