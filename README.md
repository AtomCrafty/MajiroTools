# About MajiroTool
MajiroTool is a tool designed to work with the files used by the "Majiro Script Engine", a visual novel engine. This project was a cooperation with [trigger-segfault](https://github.com/trigger-segfault/), who made a lot of very useful discoveries regarding the script format and wrote some [tools](https://github.com/trigger-segfault/majiro-py) as well as most of the [wiki](https://github.com/AtomCrafty/MajiroTools/wiki).

# About the "Majiro Script Engine"
We don't know much about the engine's history. It was developed by someone who goes by the name of Koeta Koehata (越畑声太) around 2004.

The earliest known game using the engine is [Mahjong](https://vndb.org/v1509) by NekoNeko Soft, and even newer versions of the engine still contain a lot of Mahjong specific logic. The theory is that Majiro was developed specifically for that game.  
The latest known game is [Ryakudatsusha no In'en](https://vndb.org/v20136) by Akabei Soft3, though new releases of earlier games have come out since.

## The Game Folder
Majiro games are easily identifiable, the game directory usually contains these files:
- A single game executable (`<name>.exe`)
- Several `.arc` archives with the game assets.
- A `movie` folder with any video files the game might use.
- A `savedata` folder with your settings and save data.

## Archives
The `.arc` archives contain the various game files, which include **graphics**, **audio files** and **scripts**. The name can be followed by a number from 1 to 12 if there are multiple archives with the same base name. This is the order the archives are searched in (higher numbers take precedence, so `update12.arc` has the highest priority):
- `update.arc`
- `fastdata.arc`
- `scenario.arc`
- `data.arc`
- `slowdata.arc`
- `stream.arc`
- `voice.arc`

There must exist at least one `update` or `fastdata` archive, as these are the only ones which are searched for the essential file `majiro.env` at startup. Without this configuration file, the engine fails to boot.

## Graphics
Majiro supports images in `.bmp`, `.png` and `.jpg` format, but also defines two proprietary image formats. `.rct` files contain 24 bit RGB images, while `.rc8` files contains 8 bit indexed pixel data. Since `.rct` files only contain 3 color channels, they are often accompanied by a grayscale `.rc8` file, which serves as the alpha channel. For a file `<name>.rct` the alpha file name has to be `<name>_.rc8`.

## Scripts
Majiro objects (`.mjo` files) are the binary script files controlling everything the game does.  
They are not meant to be read by humans, which is why MajiroTool will disassemble them back into a human readable format.  
The source files end in `.mjil` (Majiro intermediate language) and may be accompanied by an `.mjres` file of the same name.
These `.mjres` files use the CSV format and contain all of the text from their corresponding script.