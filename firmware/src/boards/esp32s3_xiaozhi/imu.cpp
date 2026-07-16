#include "../../hal/imu_hal.h"

// QMI8658 IMU is populated on I2C bus 0 but rotation is intentionally disabled
// (fixed-orientation panel mounting, identical rationale to AMOLED-1.8).
// board_init() has already brought Wire up on bus 0 (GPIO10/11) so the IMU is
// queryable from higher-level code if needed — we just don't drive it here.

void imu_hal_init(void) {
    // No-op: see comment above.
}

void imu_hal_tick(void) {
    // No-op.
}

uint8_t imu_hal_rotation_quadrant(void) {
    return 0;
}