#include "../../hal/power_hal.h"
#include <Arduino.h>

// No AXP2101 / battery on this board. Board is USB-powered and always-on.
// PWR button is also absent — cycle screens via touch / BOOT long-press only.

void power_hal_init(void) {
    // Nothing to bring up.
}

void power_hal_tick(void) {
    // No periodic sampling.
}

int power_hal_battery_pct(void) {
    return -1;  // unknown / not available
}

bool power_hal_is_charging(void) {
    return false;
}

bool power_hal_is_vbus_in(void) {
    // Always USB-powered on this board.
    return true;
}

bool power_hal_pwr_pressed(void) {
    return false;  // no dedicated PWR button
}

bool power_hal_pwr_long_pressed(void) {
    return false;
}

bool power_hal_pwr_released(void) {
    return false;
}