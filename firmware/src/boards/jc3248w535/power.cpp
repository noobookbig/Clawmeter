#include "../../hal/power_hal.h"

// JC3248W535C has a battery charging circuit on the schematic but
// no PMU that the firmware can read battery % from. The panel uses
// 5 V USB-C; the only "monitoring" available is VBUS detection via
// the ESP32-S3's built-in USB.
void power_hal_init(void) {}
void power_hal_tick(void) {}
int  power_hal_battery_pct(void) { return -1; }
bool power_hal_is_charging(void) { return false; }
bool power_hal_is_vbus_in(void) {
    // ESP32-S3 always powered by USB-C on this board.
    return true;
}
bool power_hal_pwr_pressed(void)        { return false; }
bool power_hal_pwr_long_pressed(void)   { return false; }
bool power_hal_pwr_released(void)       { return false; }
