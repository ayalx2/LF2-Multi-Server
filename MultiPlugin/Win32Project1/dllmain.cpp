#define WIN32_LEAN_AND_MEAN		
#include <windows.h>
#include <ddraw.h>

#include "MultiPlugin.h"
#include "DebugPrints.h"

// regular functions
void InitInstance(HANDLE hModule);
void ExitInstance(void);
void LoadOriginalDll(void);

// global variables
#pragma data_seg (".ddraw_shared")
HINSTANCE           gl_hOriginalDll;
HINSTANCE           gl_hThisInstance;
#pragma data_seg ()

BOOL APIENTRY DllMain(HANDLE hModule, DWORD  ul_reason_for_call, LPVOID lpReserved)
{
	LPVOID lpDummy = lpReserved;
	lpDummy = NULL;

	switch (ul_reason_for_call)
	{
	case DLL_PROCESS_ATTACH:
			OutputDebug("MultiPlugin - DLL_PROCESS_ATTACH");
			startupPlugin();
			InitInstance(hModule);
		break;
	case DLL_PROCESS_DETACH:
			OutputDebug("MultiPlugin - DLL_PROCESS_DETACH");
			cleanupPlugin();
			ExitInstance();
		break;
	case DLL_THREAD_ATTACH:
			OutputDebug("MultiPlugin - DLL_THREAD_ATTACH");
		break;
	case DLL_THREAD_DETACH:
			OutputDebug("MultiPlugin - DLL_THREAD_DETACH");
		break;

	}

	return(true);
}


HRESULT WINAPI DirectDrawCreate(GUID FAR *lpGUID, LPDIRECTDRAW FAR *lplpDD, IUnknown FAR *pUnkOuter)
{
	OutputDebug("MultiPlugin - DirectDrawCreate called");

	if (!gl_hOriginalDll) LoadOriginalDll(); // looking for the "right ddraw.dll"

	// Hooking DDRAW interface from Original Library IDirectDraw *pDD;

	typedef HRESULT(WINAPI* DirectDrawCreate_Type)(GUID FAR *, LPVOID *, IUnknown FAR *);

	DirectDrawCreate_Type DirectDrawCreate_fn = (DirectDrawCreate_Type)GetProcAddress(gl_hOriginalDll, ("DirectDrawCreate"));

	if (!DirectDrawCreate_fn)
	{
		::ExitProcess(0);
	}

	LPDIRECTDRAW7 FAR dummy;
	HRESULT h = DirectDrawCreate_fn(lpGUID, (LPVOID*)&dummy, pUnkOuter);

	//Ayalx: in the original code it gives a proxy interface, so the calls for the graphics will go thorugh this dll.
	//We don't need it, bacuase we use it only to load our dll.
	//So I pass the original inteface, and after this call the game will not go thorugh this dll.
	//ORIGINAL LINE: *lplpDD = (LPDIRECTDRAW) new myIDDraw7(dummy);
	*lplpDD = (LPDIRECTDRAW)dummy;

	return (h);
}

void InitInstance(HANDLE hModule)
{
	// Initialisation
	gl_hOriginalDll = NULL;
	gl_hThisInstance = NULL;

	// Storing Instance handle into global var
	gl_hThisInstance = (HINSTANCE)hModule;

	//Ayalx: prevent callings to DLL_THREAD_ATTACH / DLL_THREAD_DETACH, beascue it is unnecessary.
	//We are staticly linked to the CRT, and it can cause memory leaks in some cases.
	//but as far as i know this program does not use any of the CRT functions that uses the Local Thread Memeory.
	DisableThreadLibraryCalls((HMODULE)hModule);
}

void LoadOriginalDll(void)
{
	wchar_t buffer[MAX_PATH];

	// Getting path to system dir and to d3d9.dll
	::GetSystemDirectory(buffer, MAX_PATH);

	// Append dll name
	wcscat(buffer, L"\\ddraw.dll");

	if (!gl_hOriginalDll) gl_hOriginalDll = ::LoadLibrary(buffer);

	// Debug
	if (!gl_hOriginalDll)
	{
		OutputDebug("MultiPlugin - can not load original dll!");
		::ExitProcess(0); // exit the hard way
	}
}

void ExitInstance()
{
	if (gl_hOriginalDll)
	{
		::FreeLibrary(gl_hOriginalDll);
		gl_hOriginalDll = NULL;
	}
}

