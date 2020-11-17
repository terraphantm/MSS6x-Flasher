# MSS6x Flasher

This is a free application that can be used to read and write to the Siemens MSS60 and MSS65 DMEs found in the MY2008 - 2013 BMW M3, 2006 - 2010 BMW M5, and 2006 - 2010 M6. This application may also be compatible with specialty vehicles that also use this DME, such as the BMW Motorsport M3, however that is untested.

## Requirements:
A computer or virtual machine with .NET Framework 4.5 – I believe this means Windows 7 or newer. There may be hacks to get .NET 4.5 to work on earlier versions of Windows, but I cannot confirm their reliability.
A functional Ediabas install. Or at the very least, the following files
10Flash.prg
MS_S65, MS_S65_2, MS_S65_3, or MSS60.prg (for this application’s purposes, any of those files are sufficient for both the MSS60 and MSS65)
If you only have the PRGs and no ediabas install, you will have to make some changes to the included config file
An OBDII – USB interface
All E9x M3s and 2008+ M5s will require a K+D-CAN cable that is flashed with the EdiabasLib firmware. Bimmergeeks sells preflashed cables for a reasonable price. Without this type of cable, a DME brick is guaranteed.
2006-2007 M5s should work with a standard K-line interface, however this has not been thoroughly tested.


## Functionality:
Ability to read and write tunes and program code
An RSA bypass must be flashed to write custom tunes/programs on the MSS60 and custom programs on the MSS65 (this application will do that)
Ability to read the ISN (MSS65) / EWS Secret Key (MSS60) over OBDII
ISN requires an RSA Bypass to be written first
This means a full backup can be made entirely over OBDII
Ability to dump RAM
Automatic checksum correction for flashed tunes and programs

## Safety:
The software will verify that binaries loaded are in the correct format and order
The software will verify that programs being written match the hardware you’re trying to flash, and will verify all tunes being written match the software you’re trying to flash
If the tune or program are not stock, the program will make sure you have an RSA delete before continuing (you can override this warning, but the flash will likely fail if you don’t have an RSA delete that I couldn’t detect
The program will not let you flash an RSA bypass if the binary being loaded doesn’t match the software variant you are currently on

## Performance:
Backup tune in < 5 minutes
Full backup 45-60 minutes
2 hours in long mode
Flash tune in approximately 1 minute
Flash full program in ~10-12 minutes
Flash RSA bypass in ~15 minutes
Getting Started:
The application defaults to assuming your cable is on COM1. Make sure your port is setup correctly and your cable’s latency is set to 1ms in device manager
The application assumes your Ediabas PRG files are stored in C:\Ediabas\Ecu
The application assumes you will be using MS_S65.prg for reading and 10Flash.prg for writing
If you would like to change any of these settings, you can do so in the included .config file

## First Time Use 
### (MSS60):
Before the application lets you do anything, it has to identify what it is connected to. Connect the cable to your car and computer and hit the "Identify DME button". Now more button and menus should be active
Read a full dump from your DME. After it is done, full backup as well as a copy of your tune will be saved in a folder with the last 7 digits of your VIN. Keep that backup in a safe location.
Reads are in the Galletto V54 format if you are using any software that requires you to specify format
Click load file, and select the full dump (5120KB) you just made
Click Advanced and pick one of the RSA bypass options

Advanced Menu.jpg ​
Both slow and fast options are acceptable. Slow takes about 20 minutes instead of closer to 10.
If using a bad cable, fast will be a guaranteed permanent brick
After that process is done, you’ll be prompted to flash your tune. You could click “flash tune” now to flash back what was already there, or if you already have a modified file you want to flash, you can load that now
That’s pretty much it, you can now flash tunes and programs to your DME as you please

### (MSS65):
If you just want to flash tunes, then simply make a backup of your existing tune, modify whatever you want, and flash that back to the DME. That’s it – no patches, bypasses, etc needed.
If you want to tinker with low level code and mess with the program itself, then you will need to flash the RSA bypass like the MSS60 instructions above

## Frequently Asked Questions:

#### What is RSA and why do you need to bypass it?
RSA is an asymmetric encryption algorithm that is used in many industries to establish secure exchange of information. BMW uses it to validate tunes and code sent to the DME. Essentially BMW signs a tune or program with an encryption key that only they know. The DME can then decrypt this signature with a decryption key it has stored in memory. If a single bit in the tune or program changes, the signature is no longer valid and the DME will refuse to authenticate the program.

RSA is a secure algorithm, and for the MSS60 and MSS65, BMW uses a large enough key that it is impossible to find the key ourselves with today’s computing power. So instead we have to exploit vulnerabilities in the authentication scheme, which is what the patch this program installs does.

#### Why do I need a special cable? Isn’t my regular K+D-CAN cable good enough?
Unfortunately no. The K+DCAN cable (originally distributed by one-stop electronics and subsequently cloned by many Chinese manufacturers over the years) is a clever device that basically takes K-line messages that older BMWs used and translates them into the D-CAN format for use on 2008+ BMWs. This allowed existing software to continue working without having to rewrite anything. Unfortunately, the firmware that was developed by One-stop electronics has a bug that causes some modules on the PT-CAN bus and all modules on the MOST-bus to be programmed with erroneous data. This phenomenon was first described by Mike Benvo in 2012, where he found every 253rd byte is junk data. To my knowledge, they have yet to acknowledge the bug or issue updated firmware. So we have to replace it ourselves or buy a pre-flashed cable.

In the case of flashing the MSS6x DMEs, we are relying on some code verification mistakes that Siemens made in order to load our custom tunes and programs onto the DMEs. A side effect of relying on a verification mistake is that you lose your safety net for when junk data does actually get written. This can result in your DME no longer booting.

To add insult to injury, 2008 and newer build M3s cannot be recovered because BMW/Siemens disabled the debug port on one of the CPUs. Until the ability to unlock that port is discovered, any such DMEs that stop booting become permanent bricks.



#### Is there anything I can do safely with a standard cable?
The read functionality of the application should be safe. So if you want to make a full backup, read your secret key, or dump RAM before your new cable arrives, that should be okay.



#### I didn’t listen to you about your cable, flashed my DME, and now my car won’t start. What do I do now?
Well first of all try to see if you’re able to access it in tool32 at all. If the ident job works, then your DME can most likely be recovered. Get your hands on the proper cable and reflash your DME with WinKFP. Your DME should be recovered and should be okay to go from there

If the DME doesn’t even respond to an ident job in tool32, then unfortunately that most likely means the bootloader on your DME got erased / corrupt.

If you have an M5/M6 or a 2007 build M3, then you can recover with the use of a BDM flasher. There are several available out there. You should be able to restore your DME with the full backup that you hopefully made. If you did not make a full backup, then you can probably still recover things reasonably well, but we’ll just have to piece a few different binaries together.

If you have a 2008+ build M3, then you need to get a new DME*. Hopefully you will have made a full backup already, or at least your EWS4 secret key. If you get an MSS60 built in 2007, then you can use a BDM tool to restore the full backup from your original DME onto the replacement. The car should start afterwards.

If you can’t find a 2007 DME, then you can still make things work with a newer DME, but it becomes a little more complicated. Plug the new DME into the car, and recover the EWS Secret Key from that DME. Now you need to burn that key into your car’s CAS module. I do not have instructions on how to do that, but there are various individuals and vendors out there who should be able to do the match for you. If you’re having difficulty reaching someone, send me a message and I will try to find someone for you.


*I have now successfully unlocked a 2008+ DME, which means your original data can be cloned onto any MSS60, not just an early model. I'm still work trying to work out a way to make the process easy, but at least we know it's possible now.


#### Do you have any map packs or XDFs?
I do not at this time. It is my hope that with the release of a free application compatible with off the shelf hardware, there will be more interested parties looking into these DMEs and trying to figure things out for the sake of improving the state of community knowledge. MartynT of ECUWorx makes some excellent software that help you with some simpler tweaks to your DME.



## Acknowledgements:
This application would not be possible without EdiabasLib by uholeschak; you should check out the project here if you have any interest in BMW communications protocols
Chris325ix aka Hassmaschine aka nando for helping me get started with DME disassembly
All the testers who helped me out during the beta phase and gave me some great feedback and ideas
If you would like to be specifically named, just let me know and I'm happy to list you here
@Obioban and liam821, without whom this new forum would likely not exist.
The original M3F DME crew, which got me interested in all this stuff in the first place

## Disclosures:
I have no financial interests at this time in relation to ECU tuning, hacking, cracking, development, or disassembly.
I have no financial relationships with any of the vendors or individuals mentioned in this post

## Disclaimer:
This program is inherently invasive, and can render your DME unbootable and unrecoverable, as well as your car undriveable. Engine damage may occur as a result of this application's use. Care must be taken when using this application. In no respect shall nam3forum, m3forum.us, this program's authors or contributors incur any liability for any damages, including, but limited to, direct, indirect, special, or consequential damages arising out of, resulting from, or any way connected to the use of the application, whether or not based upon warranty, contract, tort, or otherwise; whether or not injury was sustained by persons or property or otherwise; and whether or not loss was sustained from, or arose out of, the results of, the item, or any services that may be provided by the authors and contributors.

