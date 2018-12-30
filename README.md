# perry-rhodan-audiobook-player
In F# "Fabulous" (Xamarin) written audiobook downloader and player for Perry Rhodan audiobooks.

The audiobooks were hosted by https://www.einsamedien.de/ and have to be bought on their page. (the audiobooks are in german)

You need an account there.

Perry Rhodan is the world's greatest space opera.

https://perry-rhodan.net/produkte/international

You can modify the parsing function and the login function to parse other pages.

Here is a demo video.

[![IMAGE ALT TEXT](http://img.youtube.com/vi/qgTg-DQ2ASw/0.jpg)](http://www.youtube.com/watch?v=qgTg-DQ2ASw "Perry Rhodan Audio Book Player")


2018-12-30

Detail/Description Page added for every audiop book (loading on the fly from the einsamedien page)


2018-12-28:

Using LiteDb instead of plain JSON file. More stability. Some Bugfixes. Stop playing, when remove headset or bluetooth.
Instead of downloading the zip file and extract the audio book from the file on the phone. I use a zip input stream to read extract
the audio book on the fly while downloading.
  

2018-12-11:

Demo Video added, some small stuff, bugfixing bla.



2018-12-06:

The Audioplayer currently working only on Android. I haven't implemented the audio player wrapper for iOS, yet. Nor do I own any apple devices. So community, go! 

The WPF Application is more or less only a way to test some basic functions without the long running compiling process for the android emulator.

Also there is currently no way to set the directory were the file will be stored.
