Put the Stockfish executable here as:

    Assets/StreamingAssets/Stockfish/stockfish.exe

Download: https://github.com/official-stockfish/Stockfish/releases
(tested with sf_19, stockfish-windows-x86-64-universal.zip; rename the .exe to stockfish.exe)

The .exe is ignored by git because it is larger than GitHub's 100 MB file limit, so every
checkout needs this one-time download.

The path can be changed on the ChessGame object (Computer > Stockfish Path); relative paths are
resolved against Assets/StreamingAssets, absolute paths are used as-is.

If the executable is missing or fails, the game continues with a simple built-in opponent
(Computer > Use Fallback Opponent).

Stockfish is GPLv3 licensed (see Copying.txt); if you distribute a build that bundles it,
include the license and a link to its source.
