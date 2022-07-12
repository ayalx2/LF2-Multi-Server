#define WIN32_LEAN_AND_MEAN		
#include <windows.h>

#include <stdio.h>

#include "DebugPrints.h"

#define PLAYERS_NAMES_ADDRESS (0x0044FCC0)
#define GAME_STARTED_ADDRESS (PLAYERS_NAMES_ADDRESS + 0x0BC)
#define PLAYERS_NUMBERS_ADDRESS (PLAYERS_NAMES_ADDRESS + 0xE8C)
#define RANDOM_BUFFER_ADDRESS (PLAYERS_NAMES_ADDRESS + 0x2CC + 4)

#define RANDOM_DATA_PLAYERS_NAMES_START 300
#define RANDOM_DATA_PLAYERS_NAMES_GAP 40
#define PLAYER_NAME_LENGTH 10

DWORD WINAPI MyThread(LPVOID lpParam);
bool isMultiGame(int** playerNumbers);
bool isGameStarted(int** playersNumbers);

void startupPlugin()
{
	OutputDebug("MultiPlugin 2 - start!");

	HANDLE hThread = CreateThread(
		NULL,                   // default security attributes
		0,                      // use default stack size  
		MyThread,				// thread function name
		NULL,					// argument to thread function 
		0,                      // use default creation flags 
		NULL);					// returns the thread identifier
	
	if (hThread == NULL)
	{
		OutputDebug("MultiPlugin - opening thread failed!");
		return;
	}

	CloseHandle(hThread);
}

void cleanupPlugin()
{
	OutputDebug("MultiPlugin - bye");
}

DWORD WINAPI MyThread(LPVOID lpParam)
{
	try
	{
		OutputDebug("MultiPlugin - running thread started");

		Sleep(5 * 1000);

		char* playersNames[8];
		int* playersNumbers[8];
		for (int i = 0; i < 8; ++i)
		{
			playersNames[i] = (char*)PLAYERS_NAMES_ADDRESS + i * 11;
			playersNumbers[i] = (int*)PLAYERS_NUMBERS_ADDRESS + i;
		}

		char* gameIsStarted = (char*)GAME_STARTED_ADDRESS;

		char* randomBuffer = (char*)RANDOM_BUFFER_ADDRESS;

		while (true)
		{
			OutputDebug("MultiPlugin - checking if game started");

			if (*gameIsStarted != NULL)
			{
				OutputDebug("MultiPlugin - game started!");

				//ensure lf2 is stable
				Sleep(100);

				//I think that there is a statistical bug that cause the plugin to not recognise that a multi game is running.
				//so we will run twice, in case that in the first time the memory wasn't stable.
				for (int s = 0; s < 2; ++s)
				{
					#ifdef DEBUG_PRINT
					OutputDebug("MultiPlugin - players names");
					for (int i = 0; i < 8; ++i)
					{
						OutputDebug(playersNames[i]);
					}

					OutputDebug("MultiPlugin - players numbers");
					for (int i = 0; i < 8; ++i)
					{
						char str[15];
						sprintf(str, "%d", *playersNumbers[i]);
						OutputDebug(str);
					}
					#endif

					OutputDebug("MultiPlugin - checking if multi game is running");

					//check if the connection is to multi
					if (isMultiGame(playersNumbers))
					{
						//ok. we are in multi.
						OutputDebug("MultiPlugin - multi game is running");

						for (int i = 0; i < 8; ++i)
						{
							int val = *playersNumbers[i];
							if (val != -1)
							{
								*playersNumbers[i] = 1;
							}
						}

						//Solve names for 4 other players! if players does not exists - it will include gibrish. doesn't matter.

						for (int i = 0; i < 4; ++i)
						{
							OutputDebug("iter");

							char* name = playersNames[i + 4];

							OutputDebug(name);

							memset(name, NULL, PLAYER_NAME_LENGTH);

							int index = RANDOM_DATA_PLAYERS_NAMES_START + (RANDOM_DATA_PLAYERS_NAMES_GAP * i);
							for (int t = 0, s = 0; t < PLAYER_NAME_LENGTH; ++t, s += 2)
							{
								char letter = randomBuffer[index + s];
								if (letter != NULL)
								{
									name[t] = letter;
								}
								else
								{
									break;
								}
							}
						}

						#ifdef DEBUG_PRINT
						OutputDebug("MultiPlugin - after changes!");
						OutputDebug("MultiPlugin - players names");
						for (int i = 0; i < 8; ++i)
						{
							OutputDebug(playersNames[i]);
						}

						OutputDebug("MultiPlugin - players numbers");
						for (int i = 0; i < 8; ++i)
						{
							char str[15];
							sprintf(str, "%d", *playersNumbers[i]);
							OutputDebug(str);
						}
						#endif

						break;
					}

					//wait a second and than check again if this is a multi game.
					Sleep(1 * 1000);
				}

				OutputDebug("MultiPlugin - finished");
				return 0;
			}
			
			Sleep(1 * 1000);
		}
	}
	catch (...)
	{
		//do nothing, bye
		OutputDebug("MultiPlugin - error");
	}
}

/*
Modes of players numbers i have found.

Normal: 1 2 3 4 0 0 0 0
Host: 1 2 3 4 -1 -1 -1 -1
Connect: -1 -1 -1 -1 1 2 3 4

Multi: -1 -1 -1 -1 -1 2 -1 -1

0 - the player is not in the game
-1 - the player is controlled remotely
1,2,3,4 - the player is controlled by control number x
*/
bool isMultiGame(int** playersNumbers)
{
	int nonPlayedPlayersCount = 0;
	for (int i = 0; i < 8; ++i)
	{
		int val = *playersNumbers[i];
		if (val == -1)
		{
			nonPlayedPlayersCount++;
		}
	}

#ifdef DEBUG_PRINT
		char str[50];
		sprintf(str, "nonPlayedPlayersCount: %d", nonPlayedPlayersCount);
		OutputDebug(str);
#endif

	if (nonPlayedPlayersCount == 7)
	{
		//only one player is controlled by the game. so this is a multi game!
		return true;
	}

	return false;
}



