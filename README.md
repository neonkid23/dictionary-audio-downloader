# Dictionary Audio Downloader

Windows app for downloading Dictionary.com pronunciation audio as MP3 files, with single-word, URL, and bulk queue support.
===========================

How to start
------------

1. Open the "Dictionary Audio Downloader" folder.
2. Double-click "Dictionary Audio Downloader.exe".
3. Type a word, paste a Dictionary.com word page URL, or paste an audio URL/path.
4. Press Enter or click the download arrow button.
5. Review the word list preview, then click "DOWNLOAD".


What you can enter
------------------

You can use any of these input styles:

- A single word:
  example

- Several words separated by spaces, commas, semicolons, or new lines:
  apple banana, computer; dictionary

- A Dictionary.com page URL:
  https://www.dictionary.com/browse/example

- A direct MP3 URL from Dictionary.com:
  https://assets.dictionary.com/audio/...

- A Dictionary.com audio path or data-audiosrc value:
  data-audiosrc="NEW/NEW11700.mp3"

Plain words are looked up on Dictionary.com automatically.


Queue controls
--------------

- Enter: add the current input and show the download preview.
- Shift+Enter: add the current input to the queue without downloading yet.
- Ctrl+Shift+Enter: open the larger bulk input window.
- In the bulk input window, Ctrl+Enter adds the pasted list to the queue.
- In the preview list, click "X" beside an item to remove it.
- Click "CLEAR LIST" to empty the preview list.


Buttons
-------

- Download arrow: add the typed input and open the download preview.
- X/clear button in the top bar: clear the input, queue, and log.
- Folder button in the top bar: open the output folder.
- Checkmark button after a download: reset the app back to the ready state.


Where files are saved
---------------------

Downloaded audio files are saved here:

Downloaded MP3s

This folder is inside the same folder as "Dictionary Audio Downloader.exe".
If a file with the same name already exists, the app keeps the old file and saves
the new one with a number added, such as "example-2.mp3".


Requirements
------------

- Windows.
- An internet connection.
- curl.exe must be available. It is included with modern Windows versions.


Troubleshooting
---------------

If a word fails, it usually means Dictionary.com does not have an audio file for
that word, the page layout changed, or the internet connection was interrupted.

If nothing downloads, try:

1. Confirm the word has a pronunciation audio button on Dictionary.com.
2. Try pasting the full Dictionary.com page URL instead of only the word.
3. Make sure the computer is online.
4. Make sure the "Downloaded MP3s" folder is not blocked by permissions.

The app log shows each saved file, the source URL used, and any errors for items
that could not be downloaded.
