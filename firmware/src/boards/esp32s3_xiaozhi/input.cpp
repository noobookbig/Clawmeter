#include "../../hal/input_hal.h"
#include "board.h"
#include <Arduino.h>

// Xiaozhi has one physical button (BOOT). Active-low with internal pull-up.

void input_hal_init(void) {
    pinMode(BTN_BACK_GPIO, INPUT_PULLUP);
}

bool input_hal_is_held(InputButton btn) {
    if (btn == INPUT_BTN_PRIMARY) {
        return digitalRead(BTN_BACK_GPIO) == LOW;
    }
    // Secondary button doesn't exist on this board.
    return false;
}