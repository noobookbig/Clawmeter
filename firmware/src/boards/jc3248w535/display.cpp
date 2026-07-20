#include "../../hal/display_hal.h"
#include "board.h"
#include <Arduino.h>
#include <Arduino_GFX_Library.h>

namespace {
constexpr int16_t PANEL_NATIVE_WIDTH = 320;
constexpr int16_t PANEL_NATIVE_HEIGHT = 480;
}

static Arduino_DataBus* bus = nullptr;
static Arduino_TFT*     gfx = nullptr;

// Backlight on JC3248W535C is active-high per the AXS15231B reference
// design. Keep it OFF until display_hal_begin() asserts control.
static void backlight_begin(void) {
    pinMode(LCD_BL, OUTPUT);
    digitalWrite(LCD_BL, HIGH);
}

// Hard reset the panel. RST is shared with the ESP32-S3 RST/EN domain
// in some batches, so we pulse the dedicated RST line first.
static void hard_reset_panel(void) {
    Serial.printf("RST: pin %d, drive LOW 10 ms then HIGH 120 ms\n", LCD_RESET);
    pinMode(LCD_RESET, OUTPUT);
    digitalWrite(LCD_RESET, LOW);
    delay(10);
    digitalWrite(LCD_RESET, HIGH);
    delay(120);
}

// AXS15231B init command table. The datasheet is 167 pages and the
// init sequence is buried in section 5 — these values are educated
// guesses distilled from the LCDWIKI / AXS vendor sample code. They
// may need tuning once a real panel is on the bench.
static const uint8_t axs_init_cmds[] = {
    // cmd,   count, payload bytes...
    0x01, 0,                     // SWRESET
    0x00,
    0x11, 0,                     // SLPOUT
    0x00,
    0x36, 1, 0x00,               // MADCTL: row/col layout
    0x3A, 1, 0x55,               // COLMOD: 16 bpp RGB565
    0xB0, 1, 0x68,               // RAMCTRL: refresh rate
    0xBB, 1, 0x1F,               // SETVCOM
    0xC0, 1, 0x00,               // POWER_CONTROL
    0xC2, 1, 0x33,               // POWER_CONTROL_2
    0xC3, 1, 0x1B,               // POWER_CONTROL_3
    0xC4, 1, 0xB0,               // POWER_CONTROL_4
    0xC6, 1, 0x09,               // POWER_CONTROL_6
    0xD0, 2, 0x07, 0x04,          // POWER_CONTROL_A
    0xD6, 1, 0x00,               // POWER_CONTROL_B
    0x29, 0,                     // DISPON
    0x00,
};

static void axs15231_send_init(Arduino_DataBus* b) {
    for (size_t i = 0; i < sizeof(axs_init_cmds);) {
        const uint8_t cmd  = axs_init_cmds[i++];
        const uint8_t argc = axs_init_cmds[i++];
        b->beginWrite();
        b->writeCommand(cmd);
        for (uint8_t j = 0; j < argc; j++) b->write(axs_init_cmds[i++]);
        b->endWrite();
        delay(10);
    }
}

void display_hal_init(void) {
    hard_reset_panel();
    backlight_begin();

    // 4-wire SPI bus (not QSPI). The JC3248W535C ships with the AXS15231B
    // in 3-wire SPI mode (DC tied off-chip), so we use the same SPI bus
    // but pass DC = -1 to the GFX constructor. Pin map recovered from
    // LovyanGFX issue 868 (SCK=47, MOSI=21, CS=45, RST=1).
    bus = new Arduino_ESP32SPI(LCD_DC, LCD_CS, LCD_SCLK, LCD_MOSI, LCD_MISO, FSPI, true);
    Serial.printf("SPI: CS=%d SCK=%d MOSI=%d DC=%d RST=%d\n",
                  LCD_CS, LCD_SCLK, LCD_MOSI, LCD_DC, LCD_RESET);

    // The GFX Library doesn't ship a driver for AXS15231B specifically;
    // it's register-compatible with the ST7789 family for the most part
    // (both use the same "B" / "C" page-mapped command set). Use
    // Arduino_ST7789 as a stand-in until we write a real driver.
    // TODO: when the screen actually turns on, replace this with a
    // dedicated AXS15231B driver that respects the panel's rotation
    // and inversion quirks.
    gfx = new Arduino_ST7789(
        bus, LCD_RESET, LCD_ROTATION, false,
        PANEL_NATIVE_WIDTH, PANEL_NATIVE_HEIGHT, 0, 0, 0, 0,
        st7789_type1_init_operations, sizeof(st7789_type1_init_operations));
    Serial.println("GFX: using ST7789 placeholder for AXS15231B (TODO: write real driver)");
}

void display_hal_begin(void) {
    if (!gfx) return;
    axs15231_send_init(bus);  // send our init table before the library's
    const bool ok = gfx->begin();
    Serial.printf("begin: ok=%d (rotation=%d, %dx%d)\n",
                  ok, LCD_ROTATION, LCD_WIDTH, LCD_HEIGHT);

    if (ok) {
        // Diagnostic color cycle to confirm pixels are landing on the
        // glass. 1.5 s each. Matches the LCWIKI ES3C28P diagnostic.
        Serial.println("TEST: fillScreen MAGENTA");
        gfx->fillScreen(0xF81F);
        delay(1500);
        Serial.println("TEST: fillScreen BLACK");
        gfx->fillScreen(0x0000);
        delay(1500);
        Serial.println("TEST: fillScreen RED");
        gfx->fillScreen(0xF800);
        delay(1500);
        Serial.println("TEST: fillScreen BLACK (hold)");
        gfx->fillScreen(0x0000);
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
