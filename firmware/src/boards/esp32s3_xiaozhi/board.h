#pragma once

// LCDWIKI ES3C28P — 2.8" IPS ESP32-S3 Display Module (CR2025-MI6872).
//   MCU  : ESP32-S3R8 dual-core LX7 @ 240 MHz
//   Flash: 16 MB QSPI  (4 MB variant also reported in field — see platformio.ini)
//   PSRAM: 8 MB OPI  (REQUIRED — board_build.arduino.memory_type = qio_opi)
//   LCD  : 2.8" 240x320 TFT, ILI9341V over 4-line SPI
//   Touch: D-FT6336G capacitive (FT6336-arduino compatible)
//
// Pin map from LCDWIKI ES3C28P&ES3N28P specification / Arduino demo
// instructions (CR2025-MI6140). This is the "Xiaozhi Type C" form factor
// sold by LCDWIKI and is NOT pin-compatible with Waveshare's
// ESP32-S3-Touch-LCD-2.8 (which uses GPIO 40/41/42/45 for SPI).

#define BOARD_NAME           "LCDWIKI ES3C28P (Xiaozhi Type C)"

// ---- Display geometry ----
// Native 240x320 portrait; build_src_filter + BOARD_LCD_LANDSCAPE env
// decide the runtime rotation.
#ifdef BOARD_LCD_LANDSCAPE
#define LCD_WIDTH            320
#define LCD_HEIGHT           240
#define LCD_ROTATION         1
#else
#define LCD_WIDTH            240
#define LCD_HEIGHT           320
#define LCD_ROTATION         0
#endif

// ---- SPI TFT display (ILI9341V) ----
// Per LCDWIKI ES3C28P spec, the SPI bus is routed to the ESP32-S3's
// "fast" pins (IO10..IO13) — the IO_MUX routes these to the FSPI
// peripheral automatically, so FSPI still works.
#define LCD_DC               46   // TFT_RS (data/command)
#define LCD_CS               10   // TFT_CS (chip select)
#define LCD_SCLK             12   // TFT_SCK (SPI clock)
#define LCD_MOSI             11   // TFT_MOSI (SPI MOSI)
#define LCD_MISO             13   // TFT_MISO (SPI MISO; used for panel ID read)
#define LCD_RESET            21   // CHIP_PU on the spec means the LCD's RST is
                                 // shared with the ESP32-S3 EN line. Toggling
                                 // GPIO21 asserts the chip-PU reset domain. If
                                 // your batch lacks that strap, set this to -1
                                 // and rely on the SWRESET 0x01 sequence.
#define LCD_BL               45   // TFT_BL (high = backlight on)

// ---- Capacitive touch (FT6336G on its own I2C bus) ----
// On the ES3C28P the touch controller is on I2C bus 0 (GPIO 15/16), shared
// with the on-board audio DAC and the (optional) IMU/RTC. We bring that
// bus up in board_init() and use Wire (not Wire1) for the touch driver.
#define TP_SDA               16
#define TP_SCL               15
#define TP_INT               17
#define TP_RST               18
#define FT6336_ADDR          0x38   // FocalTech-style default

// ---- I2C bus 0 (shared: touch + audio DAC + (optional) RTC) ----
#define IIC0_SDA             16
#define IIC0_SCL             15

// ---- Buttons ----
#define BTN_BACK_GPIO        0     // BOOT — primary HID Space; also hold-to-pair
// No secondary button on this board.

// ---- Capability flags ----
// PSRAM is populated — splash canvas + double LVGL draw buffers go there.
// ILI9341V cannot rotate natively, but for now we run at the panel's
// natural orientation (no IMU).
// No AXP2101 PMU on this board → no battery percentage, always-on.
#define BOARD_HAS_SECONDARY_BUTTON 0
#define BOARD_HAS_ROTATION         0
#define BOARD_HAS_IMU              0
#define BOARD_HAS_BATTERY          0
#define BOARD_HAS_IO_EXPANDER      0
#define BOARD_HAS_ALWAYS_ON        1
#define BOARD_HAS_NEON_THEME       1   // 240x320 ILI9341V → banding-prone → neon
#define BOARD_HAS_PSRAM            1   // 8 MB OPI PSRAM (qio_opi)