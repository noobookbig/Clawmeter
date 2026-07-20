#include "board.h"
#include <Arduino.h>
#include <Wire.h>

// Called once at the start of setup() before any HAL device init.
// Brings up Wire on the shared I2C bus and asserts the panel's reset
// line so it's in a known state by the time display_hal_init() runs.
extern "C" void board_init(void) {
    // Reset the panel. Some batches don't expose RST to a GPIO so we
    // use the shared I/O that maps to AXS15231B RSTN.
    pinMode(LCD_RESET, OUTPUT);
    digitalWrite(LCD_RESET, LOW);
    delay(10);
    digitalWrite(LCD_RESET, HIGH);
    delay(120);  // AXS15231B boot window

    // Backlight pin is active-high per the AXS15231B reference design.
    // Keep it OFF until display_hal_begin() takes over.
    pinMode(LCD_BL, OUTPUT);
    digitalWrite(LCD_BL, LOW);

    // BOOT button is the primary HID trigger.
    pinMode(BTN_BACK_GPIO, INPUT_PULLUP);

    // Single shared I2C bus for touch + (any future) audio / RTC.
    // The touch subsystem on AXS15231B and any external I2C device
    // share this bus. Wire.begin() defaults to the platform I2C clock
    // which the touch driver's 400 kHz request will bump anyway.
    Wire.begin(IIC0_SDA, IIC0_SCL);
}
