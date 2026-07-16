#pragma once
#include <stdint.h>

// Runtime board description consumed by board-agnostic code (UI, main loop).
// Each board provides a single BoardCaps instance via board_caps().
//
// Compile-time-only facts (pin numbers, library choice) belong in
// boards/<name>/board.h and never leak into shared code. Anything the UI or
// main loop needs at runtime — display size, optional-feature presence —
// goes here so shared code stays free of #ifdef BOARD_*.
struct BoardCaps {
    const char* name;        // human-readable, e.g. "Waveshare AMOLED 2.16"

    int16_t width;           // active display width in pixels
    int16_t height;          // active display height in pixels

    uint8_t button_count;    // 1 = primary (BOOT) only; 2 = primary + secondary
    bool    has_rotation;    // IMU-driven CPU rotation in the flush callback
    bool    has_battery;     // AXP2101 battery measurement is meaningful
    bool    has_imu;         // QMI8658 (or compatible) is populated
    bool    always_on;       // true = board never sleeps (no battery, USB always on)
    bool    neon_theme;      // 013 landscape neon-glow theme (cyan/magenta,
                             // banding-free). Only meaningful in landscape.
    bool    has_psram;       // true = OPI/QSPI PSRAM populated. Lets the shared
                             // code render feature buffers (e.g. screen grid)
                             // that would otherwise overflow CYD's 320 KB DRAM.
};

const BoardCaps& board_caps(void);
