#include "board.h"
#include <Arduino.h>
#include <Wire.h>

// LCDWIKI ES3C28P only has ONE I2C bus (bus 0, GPIO 15/16). The touch
// controller (FT6336G) shares the bus with the on-board audio DAC and
// the (optional) RTC chip. board_init() brings the shared Wire bus up
// once with the TP_SDA / TP_SCL pins; touch.cpp / power.cpp / imu.cpp
// re-use it for their devices.
extern "C" void board_init(void) {
    // Backlight gate — keep off until display_hal_begin() drives it.
    pinMode(LCD_BL, OUTPUT);
    digitalWrite(LCD_BL, LOW);

    // Touch reset line — release after a short pulse so FT6336G boots cleanly.
    pinMode(TP_RST, OUTPUT);
    digitalWrite(TP_RST, LOW);
    delay(10);
    digitalWrite(TP_RST, HIGH);
    delay(50);

    // Primary button (BOOT) — active LOW on every ESP32-S3 dev board.
    pinMode(BTN_BACK_GPIO, INPUT_PULLUP);

    // Single shared I2C bus on GPIO 15/16 (TP_SCL / TP_SDA).
    Wire.begin(IIC0_SDA, IIC0_SCL);
}