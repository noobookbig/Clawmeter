#include "../../hal/display_hal.h"
#include "board.h"
#include <Arduino.h>
#include <Arduino_GFX_Library.h>

namespace {
constexpr int16_t PANEL_NATIVE_WIDTH = 240;
constexpr int16_t PANEL_NATIVE_HEIGHT = 320;
}

static Arduino_DataBus* bus = nullptr;
static Arduino_TFT*     gfx = nullptr;

// Backlight on the LCDWIKI ES3C28P is active-HIGH (TFT_BL = GPIO45).
static void backlight_begin(void) {
    pinMode(LCD_BL, OUTPUT);
    digitalWrite(LCD_BL, HIGH);
}

// LCDWIKI ES3C28P routes LCD_RST to GPIO21 (CHIP_PU — the ESP32-S3's
// chip-pull-up reset domain). We pulse it low then release before the
// GFX library sends its init ops.
static void hard_reset_panel(void) {
    pinMode(LCD_RESET, OUTPUT);
    digitalWrite(LCD_RESET, LOW);
    delay(10);
    digitalWrite(LCD_RESET, HIGH);
    delay(120);
}

void display_hal_init(void) {
    hard_reset_panel();
    backlight_begin();

    // GFX Library v1.6.4 exposes only the 7-arg constructor:
    //   (dc, cs, sck, mosi, miso, spi_num, is_shared_interface) = true).
    // spi_num = FSPI (0) on ESP32-S3 maps the call to the SPI2 peripheral
    // and the IO_MUX routes GPIO 10..13 to the FSPI data pins.
    bus = new Arduino_ESP32SPI(LCD_DC, LCD_CS, LCD_SCLK, LCD_MOSI, LCD_MISO, FSPI, true);
    // Cap SPI clock at 27 MHz — the GFX library default on S3 is 80 MHz
    // which corrupts writes on several ES3C28P batches. 27 MHz matches
    // the demo instructions and is conservative enough for the panel.
    SPI.setFrequency(27000000);

#if defined(CYD_LCD_ST7789)
    gfx = new Arduino_ST7789(
        bus, LCD_RESET, LCD_ROTATION, false,
        PANEL_NATIVE_WIDTH, PANEL_NATIVE_HEIGHT, 0, 0, 0, 0,
        st7789_type1_init_operations, sizeof(st7789_type1_init_operations));
#elif defined(CYD_LCD_ILI9341_TYPE2)
    gfx = new Arduino_ILI9341(
        bus, LCD_RESET, LCD_ROTATION, false,
        PANEL_NATIVE_WIDTH, PANEL_NATIVE_HEIGHT, 0, 0, 0, 0,
        ili9341_type2_init_operations, sizeof(ili9341_type2_init_operations));
#else
    // Default: ES3C28P is an ILI9341V panel per LCDWIKI spec. type1 init
    // ops (the same as the Sunton CYD R) work on every batch we tested.
    gfx = new Arduino_ILI9341(
        bus, LCD_RESET, LCD_ROTATION, false,
        PANEL_NATIVE_WIDTH, PANEL_NATIVE_HEIGHT, 0, 0, 0, 0,
        ili9341_type1_init_operations, sizeof(ili9341_type1_init_operations));
#endif
}

// Panel colour inversion. The captured LVGL framebuffer is correct, but this
// ES3C28P batch shows the dark UI as white with every colour complemented
// (dark↔light, cyan↔red, etc.) — classic INVON/INVOFF mismatch. Sending
// INVON after begin() flips the panel back to normal. Overridable via a build
// flag in case a future batch ships the opposite polarity.
#ifndef XIAOZHI_PANEL_INVERT
#define XIAOZHI_PANEL_INVERT 1
#endif

// Try 18-bit RGB666 (262 K colours) per the ES3C28P spec. The ILI9341V
// panel supports three pixel formats via register 0x3A (COLMOD):
//   0x55 = 16 bpp RGB565 (default — GFX Library + LVGL default)
//   0x66 = 18 bpp RGB666 (262K colours per the LCDWIKI spec sheet)
//   0x33 = 12 bpp RGB444 (rare)
// We send 0x3A 0x66 after begin() to switch the panel. The GFX Library
// still writes 16-bit pixels, which the panel zero-pads in the upper
// 2 bits per channel — visually identical to RGB565 but the panel's
// internal gamma tables are now configured for 18-bit input.
//
// Set XIAOZHI_PANEL_RGB666 to 1 to enable, 0 to disable.
#ifndef XIAOZHI_PANEL_RGB666
#define XIAOZHI_PANEL_RGB666 1
#endif

void display_hal_begin(void) {
    if (!gfx) return;
    gfx->begin();
    gfx->invertDisplay(XIAOZHI_PANEL_INVERT ? true : false);

    if (XIAOZHI_PANEL_RGB666) {
        // Switch panel to RGB666 (18 bpp) per spec. Use the GFX bus
        // directly so we don't have to plumb writeCommand through a
        // separate public API.
        bus->beginWrite();
        bus->writeCommand(0x3A);  // COLMOD
        bus->write(0x66);         // 18 bits/pixel
        bus->endWrite();
        Serial.println("DISPLAY: switched ILI9341V to RGB666 18 bpp (per spec)");
    }
}

void display_hal_set_brightness(uint8_t level) {
    digitalWrite(LCD_BL, level > 0 ? HIGH : LOW);
}

void display_hal_fill_screen(uint16_t color) {
    if (gfx) gfx->fillScreen(color);
}

void display_hal_draw_bitmap(int32_t x, int32_t y, int32_t w, int32_t h,
                             const uint16_t* pixels) {
    if (gfx) gfx->draw16bitRGBBitmap(x, y, (uint16_t*)pixels, w, h);
}

void display_hal_tick(void) {
    // No IMU-driven rotation on this board.
}

void display_hal_round_area(int32_t* x1, int32_t* y1, int32_t* x2, int32_t* y2) {
    (void)x1;
    (void)y1;
    (void)x2;
    (void)y2;
}