# MSS6x Flasher

This is a free application that can be used to read and write to the Siemens MSS60 and MSS65 DMEs found in the MY2008 - 2013 BMW M3, 2006 - 2010 BMW M5, and 2006 - 2010 M6. This application may also be compatible with specialty vehicles that also use this DME, such as the BMW Motorsport M3, however that is untested.

## Requirements:
* A computer or virtual machine with .NET Framework 4.8.1. Windows 11 for ARM is supported.
* A functional Ediabas install. Or at the very least, the following files
* 10Flash.prg (other "XFlash.prg" files may work, but untested)
* MS_S65, MS_S65_2, MS_S65_3, or MSS60.prg (for this application’s purposes, any of those files are sufficient for both the MSS60 and MSS65)
	* If you only have the PRGs and no ediabas install, you will have to make some changes to the included config file
* An OBDII – USB interface
	* As of version 1.1.0, an unmodified K+D-CAN cable can be used for all functions on all cars. However it is strongly recommended you use a cable with the EdiabasLib firmware on D-CAN cars (all M3s, LCI M5s). Such a cable is still required for WinKFP with these DMEs
* 2006-2007 M5s should work with a standard K-line (non-CAN) interface, however this has not been thoroughly tested.


## Functionality:
* Ability to read and write tunes and program code
* An RSA bypass must be flashed to write custom tunes on the MSS60 with software newer than 140E and custom programs on all DMEs MSS65 (this application will do that)
* Ability to read the ISN (MSS65 - requires RSA bypass) / EWS Secret Key (MSS60) over OBDII
	* This means a full backup can be made entirely over OBDII
* Ability to dump RAM
* Automatic checksum correction for flashed tunes and programs

## Safety:
* The software will verify that binaries loaded are in the correct format and order
* The software will verify that programs being written match the hardware you’re trying to flash, and will verify all tunes being written match the software you’re trying to flash
* If the tune or program are not stock, the program will make sure you have an RSA delete before continuing (you can override this warning, but the flash will likely fail if you don’t have an RSA delete that I couldn’t detect)
	* The program will not let you flash an RSA bypass if the binary being loaded doesn’t match the software variant you are currently on

## Performance:
* Backup tune in ~3 minutes
* Full backup ~25 minutes (for 241E)
	* ~1 hours in long mode (not thoroughly tested, should not be needed in most cases)
* Flash tune in < 1 minute
* Flash full program or RSA bypass in ~7 minutes
* All times assume a reasonably quick computer and using K-line or EdiabasLib flashed cable on D-CAN cars. Standard firmware is much slower. YMMV

## Getting Started:
* The application defaults to assuming your cable is on COM1. Make sure your port is setup correctly and your cable’s latency is set to 1ms in device manager
* The application assumes your Ediabas PRG files are stored in C:\Ediabas\Ecu
* The application assumes you will be using MS_S65.prg for reading and 10Flash.prg for writing
* If you would like to change any of these settings, you can do so in the included .config file using a text editor

## First Time Use 
### (MSS60 ≥ 200E):
* Before the application lets you do anything, it has to identify what it is connected to. Connect the cable to your car and computer and hit the "Identify DME button". Now more button and menus should be active
* Read a full dump from your DME. After it is done, full backup as well as a copy of your tune will be saved in a folder with the last 7 digits of your VIN. Keep that backup in a safe location.
* Reads are in the Galletto V54 format if you are using any software that requires you to specify format
	* As of version 1.1.0, BDM format backups are also produced. These can be used to recover bricks using a BDM device, such as the ULink NT sold by usbjtag
* Click load file, and select the full dump (5120KB) you just made
* Click Advanced and choose the RSA bypass option. 
* After it is complete, you can flash custom tunes and programs as you please.
* If you would like to restore the stock file (and remove the RSA bypass and other patches), load your backup and choose the "Restore Stock" option.

### (MSS60 <140E , All MSS65):
* If you just want to flash tunes, then simply make a backup of your existing tune, modify whatever you want, and flash that back to the DME. That’s it – no patches, bypasses, etc needed.
* If you want to tinker with low level code and mess with the program itself, or if you wish to read the ISN on the MSS65, then you will need to flash the RSA bypass as described in the instructions above

## Frequently Asked Questions:

#### What is RSA and why do you need to bypass it?
* RSA is an asymmetric encryption algorithm that is used in many industries to establish secure exchange of information. BMW uses it to validate tunes and code sent to the DME. Essentially BMW signs a tune or program with an encryption key that only they know. The DME can then decrypt this signature with a decryption key it has stored in memory. If a single bit in the tune or program changes, the signature is no longer valid and the DME will refuse to authenticate the program.

* RSA is a secure algorithm, and for the MSS60 and MSS65, BMW uses a large enough key that it is impossible to find the key ourselves with today’s computing power. So instead we have to exploit vulnerabilities in the authentication scheme, which is what the patch this program installs does.

#### Why do I need a special cable? Isn’t my regular K+D-CAN cable good enough?
* As of version 1.1.0, you can use a standard K+DCAN cable, however it is not recommended as it is slower and would still result in bricks when using WinKFP

* The common K+DCAN cable(originally distributed by one-stop electronics and subsequently cloned by many Chinese manufacturers) is a clever device that takes K-line messages that older BMWs used and translates them into the D-CAN format for use on 2008+ BMWs. This allowed existing software to continue working without having to rewrite anything. 
* Unfortunately, the firmware that was developed by One-stop electronics has a bug that causes some modules programmed with erroneous data. This can cause the DME to no longer boot and require a BDM interface to recover.
* It is now known how to disable the BDM lock that the MSS60s have, and cheap debuggers exist, so the bricks are no longer as catastrophic as they used to be.  

#### I managed to brick my DME, what can I do?
* Try to see if you’re able to access it in tool32 at all. If the ident job works, then your DME can most likely be recovered. Get your hands on an ediabaslib cable and reflash your DME with WinKFP. Your DME should be recovered and should be okay to go from there

* If the DME doesn’t even respond to an ident job in tool32, then unfortunately that most likely means the bootloader on your DME got erased / corrupt.

* If you have a full backup, you can restore a backup with a BDM device. I recommend the Ulink NT sold at usbjtag.com (I am not affiliated with them in anyway). 
	* Most MSS60s have the injection CPU in 'censorship mode' -- this will require you to manipulate some registers to clear. This will erase the entirety of the flash, but if you have a full backup, this is not a large concern.
* If you do not have a full have a full backup, you will need to construct a file suitable for your car.
	* In the case of the MSS60, you will also either need to recover your EWS Secret Key from your CAS module or flash an EWS delete

#### Do you have any map packs or XDFs?
* I do not at this time. You can check nam3forum and other BMW forums to see if any have since been released


## Acknowledgements:
* This application would not be possible without EdiabasLib by uholeschak; you should check out the project here if you have any interest in BMW communications protocols
* Chris325ix aka Hassmaschine aka nando for helping me get started with DME disassembly
* All the testers who helped me out during the beta phase and gave me some great feedback and ideas
* If you would like to be specifically named, just let me know and I'm happy to list you here
* @Obioban and liam821, without whom this new forum would likely not exist.
* The original M3F DME crew, which got me interested in all this stuff in the first place

## Disclosures:
* I have no financial interests at this time in relation to ECU tuning, hacking, cracking, development, or disassembly.
* I have no financial relationships with any of the vendors or individuals mentioned in this post

## Disclaimer:
This program is inherently invasive, and can render your DME unbootable and unrecoverable, as well as your car undriveable. Engine damage may occur as a result of this application's use. Care must be taken when using this application. In no respect shall nam3forum, m3forum.us, this program's authors or contributors incur any liability for any damages, including, but limited to, direct, indirect, special, or consequential damages arising out of, resulting from, or any way connected to the use of the application, whether or not based upon warranty, contract, tort, or otherwise; whether or not injury was sustained by persons or property or otherwise; and whether or not loss was sustained from, or arose out of, the results of, the item, or any services that may be provided by the authors and contributors.

