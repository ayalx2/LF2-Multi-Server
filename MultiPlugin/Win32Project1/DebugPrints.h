#pragma once

//uncomment this line to enable debug prints
//#define DEBUG_PRINT

#ifdef DEBUG_PRINT
#define OutputDebug(x) OutputDebugStringA(x)
#else
#define OutputDebug(x)
#endif