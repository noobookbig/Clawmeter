#include "../../hal/input_hal.h"
#include "board.h"
#include <Arduino.h>

// One physical button (BOOT) on GPIO 0. Active-low with internal pull-up.
void input_hal_init(void) {
    pinMode(BTN_BACK_GPIO, INPUT_PULLUP);
}

bool input_hal_is_held(InputButton btn) {
    if (btn == INPUT_BTN_PRIMARY) {
        return digitalRead(BTN_BACK_GPIO) == LOW;
    }
    return false;
}
