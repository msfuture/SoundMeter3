# SoundMeter3

WinUI3 App für die Verbindung zu einem günstigen Bluetooth dB-Meter (https://www.amazon.de/dp/B0C85RSN4N). 
Die App sucht ein Gerät namens "SoundMeter", baut eine Bluetooth Verbindung auf und schreib nach Start regelmäßig 0x303b an den Service fff2, um dann auf dem Service fff1 die aktuellen Messwerte auszulesen.
Die Daten werden als Zahl und einfachem Graph ausgegeben.

Dies ist nur ein Projekt um die Verbindung zu testen und eine Library für ein anderes Projekt zu entwerfen. Du willst trotzdem mehr aus dieser App machen? Ich nehme gerne Pull requests an.
