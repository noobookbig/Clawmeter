#pragma once

// JC3248W535C_I_Y — 3.5" ESP32-S3 module (Shenzhen JingCai Smart).
//   MCU  : ESP32-S3-WROOM-1 (dual-core LX7 @ 240 MHz)
//   Flash: 16 MB QSPI
//   PSRAM: 8 MB OPI  (REQUIRED — qio_opi for splash + double LVGL buffers)
//   LCD  : 3.5" 320×480 IPS TFT via QSPI (4 data lanes)
//          Driver = AXS15231B (in-cell IC integrates 540-channel
//          6-bit source driver + GIP gate driver + touch panel
//          controller). Capable of 16-bit RGB565.
//   Touch: built-in to AXS15231B over I2C (no separate controller).
//
// Pin map below is a best-guess from the schematic. The user must
// confirm against the actual PCB before flashing — the JC3248W535C
// has multiple variants in the field and the silk-screen may differ
// from the schematic you provided.

#define BOARD_NAME           "JC3248W535C (3.5\" QSPI)"

// ---- Display geometry ----
// 320×480 in native portrait; landscape envs flip via BOARD_LCD_LANDSCAPE.
#ifdef BOARD_LCD_LANDSCAPE
#define LCD_WIDTH            480
#define LCD_HEIGHT           320
#define LCD_ROTATION         1
#else
#define LCD_WIDTH            320
#define LCD_HEIGHT           480
#define LCD_ROTATION         0
#endif

// ---- SPI display (AXS15231B) ----
// Pin map recovered from LovyanGFX issue 868 — the JC3248W535C uses
// plain 4-wire SPI on these GPIOs (DC not needed on the 3-wire path
// AXS15231B exposes, but we'll use 4-wire mode for simplicity).
// Confirmed working values: SCK=47, MOSI=21, CS=45, RST=1.
#define LCD_DC               -1   // 3-wire SPI mode (DC not used)
#define LCD_CS               45
#define LCD_SCLK             47
#define LCD_MOSI             21
#define LCD_MISO             -1
#define LCD_RESET            1
#define LCD_BL               38   // user tried 1, 4, 38; 38 likely correct
                                  // (Schematic labels the gate signal for
                                  // the backlight driver as LCD_BL @ Q5 BSS138)

// ---- Touch (AXS15231B built-in I2C) ----
// I2C bus is shared with the rest of the board. TODO: confirm the
// actual address by reading 0x00 (chip-id) — the AXS15231B touch
// subsystem is often at 0x14 but some batches are at 0x5D.
#define TP_SDA               47   // shared with LCD SCLK — see TODO
#define TP_SCL               44   // shared with potential LCD D3
#define TP_INT               38
#define TP_RST               48
#define FT6336_ADDR          0x14  // best-guess for AXS15231B touch subsystem
// TODO: confirm 0x14 vs 0x5D by reading 0x00

// ---- I2C bus (shared with touch + any audio DAC + RTC) ----
#define IIC0_SDA             47
#define IIC0_SCL             44

// ---- Buttons ----
#define BTN_BACK_GPIO        0     // BOOT — primary HID Space; also hold-to-pair
// No secondary button on this board.

// ---- Capability flags ----
#define BOARD_HAS_SECONDARY_BUTTON 0
#define BOARD_HAS_ROTATION         0
#define BOARD_HAS_IMU              0
#define BOARD_HAS_BATTERY          0
#define BOARD_HAS_IO_EXPANDER      0
#define BOARD_HAS_ALWAYS_ON        1
#define BOARD_HAS_NEON_THEME       1
#define BOARD_HAS_PSRAM            1
