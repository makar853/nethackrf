# NetHackrf
This project allows to control HackRF tranceivers using .net environment.

# Usage

Firstly you need to get list of connected hackrf devices by using **NetHackrf.HackrfDeviceList()** which returns array of **NetHackrf.hackrf_device_info** objects.<br>

Each **NetHackrf.hackrf_device_info** object has **OpenDevice()** method which returns **NetHackrf** object.<br>

To start receiving or transmitting data you need to run **StartRX()** or **StartTX()** method of **NetHackrf** object which would return System.IO.Stream object. Stream object is used to write or read IQ interleaved data.<br>

Hackrf is half-duplex device thus only one stream can be used at a time. Before using **StartRX()** or **StartTX()** methods again you should stop the existing stream by using it's **Dispose()** method.<br>

You can control transcevier by writing **NetHackrf** class properties which are:<br>
 * double **FilterBandwidthMHz**
 * double **CarrierFrequencyMHz**
 * double **SampleFrequencyMHz**
 * bool **AntPower**
 * bool **ClkOut**
 * double **LNAGainDb**
 * double **VGAGainDb**
 * double **TXVGAGainDb**
 * bool **AMPEnable**


# About
This project uses precompiled dynamic link libraries which have been released under GNU LGPL v2.1 license and GNU GPL v2 license: <br>
libusb https://github.com/libusb/libusb <br>
hackrf https://github.com/mossmann/hackrf/
