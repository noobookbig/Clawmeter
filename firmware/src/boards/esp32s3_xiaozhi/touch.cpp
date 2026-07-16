#include "../../hal/touch_hal.h"
#include "board.h"
#include <Arduino.h>
#include <Wire.h>

// Minimal FT6336G capacitive-touch reader on the ES3C28P's shared I2C bus.
//
// On the LCDWIKI ES3C28P the FT6336G lives on the SAME I2C bus as the
// audio DAC / (optional) RTC — GPIO 15 (SCL) and GPIO 16 (SDA). Wire
// is already brought up by board_init() with those pins, so this driver
// just uses the global Wire instance and waits for the TP_INT line.
//
// FT6336G register map (FocalTech-style, same as FT3168 / FT5x06):
//   reg 0x02        : low nibble = active touch point count
//   reg 0x03 / 0x04 : X1 high (low nibble) + X1 low  → 12-bit X
//   reg 0x05 / 0x06 : Y1 high (low nibble) + Y1 low  → 12-bit Y
// Touch resolution is 0..4095 (12-bit) but the panel is 240x320; we
// clamp to those bounds before handing off to LVGL.

static volatile bool   touch_data_ready = false;
static volatile bool   touch_pressed = false;
static volatile uint16_t touch_x = 0;
static volatile uint16_t touch_y = 0;

static void IRAM_ATTR touch_isr(void) {
    touch_data_ready = true;
}

static void touch_pump(void) {
    Wire.beginTransmission(FT6336_ADDR);
    Wire.write(0x02);
    if (Wire.endTransmission(false) != 0) { touch_pressed = false; return; }
    if (Wire.requestFrom(FT6336_ADDR, (uint8_t)5) != 5) {
        touch_pressed = false; return;
    }
    const uint8_t fingers = Wire.read() & 0x0F;
    const uint8_t xH = Wire.read();
    const uint8_t xL = Wire.read();
    const uint8_t yH = Wire.read();
    const uint8_t yL = Wire.read();
    if (fingers == 0 || fingers > 5) {
        touch_pressed = false;
        return;
    }
    uint16_t x = ((uint16_t)(xH & 0x0F) << 8) | xL;
    uint16_t y = ((uint16_t)(yH & 0x0F) << 8) | yL;
    // FT6336 reports up to 4096 even on a 240x320 panel; clamp to panel size.
    if (x >= LCD_WIDTH)  x = LCD_WIDTH  - 1;
    if (y >= LCD_HEIGHT) y = LCD_HEIGHT - 1;
    touch_x = x;
    touch_y = y;
    touch_pressed = true;
}

void touch_hal_init(void) {
    // Wire is already up (board_init called Wire.begin(IIC0_SDA, IIC0_SCL)).
    // Verify the chip is alive on the expected address.
    Wire.beginTransmission(FT6336_ADDR);
    Wire.write(0xA0);  // FT6336 chip-id register
    if (Wire.endTransmission(false) == 0 &&
        Wire.requestFrom(FT6336_ADDR, (uint8_t)1) == 1) {
        Serial.printf("Touch FT6336G ID=0x%02X (addr 0x%02X, SDA=%d SCL=%d)\n",
                      Wire.read(), FT6336_ADDR, TP_SDA, TP_SCL);
    } else {
        Serial.printf("Touch FT6336G ID read failed (addr 0x%02X, SDA=%d SCL=%d)\n",
                      FT6336_ADDR, TP_SDA, TP_SCL);
    }

    pinMode(TP_INT, INPUT_PULLUP);
    attachInterrupt(TP_INT, touch_isr, FALLING);
    Serial.println("Touch attached on INT pin (shared Wire bus)");
}

void touch_hal_read(uint16_t* x, uint16_t* y, bool* pressed) {
    if (touch_data_ready) {
        touch_data_ready = false;
        touch_pump();
    }
    *x = touch_x;
    *y = touch_y;
    *pressed = touch_pressed;
}