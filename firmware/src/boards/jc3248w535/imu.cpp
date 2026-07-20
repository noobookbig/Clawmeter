#include "../../hal/imu_hal.h"

// JC3248W535C doesn't expose a separate IMU chip (the schematic
// only lists the ESP32-S3, AXS15231B display/touch combo, and passives).
// Keep the API stub so the rest of the firmware can compile unchanged.
void imu_hal_init(void) {}
void imu_hal_tick(void) {}
uint8_t imu_hal_rotation_quadrant(void) { return 0; }
