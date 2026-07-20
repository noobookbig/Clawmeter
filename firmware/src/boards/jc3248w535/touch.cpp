#include "../../hal/touch_hal.h"
#include "board.h"
#include <Arduino.h>
#include <Wire.h>

// JC3248W535C_I_Y uses the touch subsystem built into the AXS15231B
// display driver. It is reachable over the same I2C bus as any
// other peripheral (Wire), and the registers follow the FT6336/FT5x06
// family (AXS15231B is a second-source clone).
//
// Register map (FT6336-compatible):
//   reg 0x02        : low nibble = active touch point count
//   reg 0x03 / 0x04 : X1 high (low nibble) + X1 low  → 12-bit X
//   reg 0x05 / 0x06 : Y1 high (low nibble) + Y1 low  → 12-bit Y
// Touch resolution is reported in 0..4095 (12-bit) but the panel is
// 320×480; we clamp to those bounds before handing off to LVGL.
//
// IMPORTANT: the schematic shows the touch I2C on the same SDA/SCL
// as Wire (board_init does Wire.begin(IIC0_SDA, IIC0_SCL)), so the
// driver uses the global Wire instance.

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
    // AXS15231B touch reports up to 4096 even on a 320×480 panel; clamp.
    if (x >= LCD_WIDTH)  x = LCD_WIDTH  - 1;
    if (y >= LCD_HEIGHT) y = LCD_HEIGHT - 1;
    touch_x = x;
    touch_y = y;
    touch_pressed = true;
}

void touch_hal_init(void) {
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
