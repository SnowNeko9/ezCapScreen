# ezCapScreen

ezCapScreen is a small Windows screenshot tool. Press a shortcut, drag over part of the screen, mark it up, and copy or save the result.

[อ่านภาษาไทย](#ภาษาไทย)

## Features

- Select a region on any monitor, or across monitors. Move and resize it with the handles.
- Draw rectangles, ovals, arrows, and brush strokes in 39 colors and 3 sizes.
- Type text in English or Thai.
- Copy the result to the clipboard, or save it as PNG, JPEG, or BMP.
- Change the capture shortcut, and turn on start with Windows.
- Switch the interface between English and Thai.
- Follow the Windows light or dark theme, and scale to each monitor.

## Requirements

- Windows 10 version 1703 or later. Testing so far has been on Windows 11 only.
- .NET Framework 4.x, which Windows 10 and Windows 11 include.

## Install and run

1. Download `ezCapScreen-Setup.exe` from the [Releases page](../../releases).
2. Run the file and follow the steps. You do not need administrator rights.
3. Start ezCapScreen from the Start menu. An icon appears in the notification area of the taskbar.

To remove the program, open **Settings > Apps > Installed apps** in Windows and uninstall ezCapScreen. This also removes its settings.

To use it without installing, download `ezCapScreen.exe` from the same page and run it.

The files have no digital signature, so Windows SmartScreen may show a warning. If you prefer, [build it from source](#build-from-source).

Microsoft Defender flagged version 1.0.0 as `Behavior:Win32/Persistence.A!ml`, most likely because that version rewrote its start-with-Windows entry every time it started. Version 1.0.1 writes the entry only when you change the option or choose it during setup. Defender could still warn when this happens.

## Take a screenshot

1. Press `Ctrl+Alt+A`, or click the notification area icon.
2. Drag over the area you want. To start again, drag somewhere outside the selected area.
3. To mark up the image, choose a tool on the toolbar, then a size and a color. `Ctrl+Z` removes the last mark, and `Ctrl+Y` brings it back.
4. To finish, choose one of these:
   - The check mark or `Enter` copies the image to the clipboard.
   - The disk icon saves the image to a file.
   - The X or `Esc` closes without saving.

If another app already uses `Ctrl+Alt+A`, ezCapScreen tells you. You can still click the icon, or choose another shortcut in **Settings**.

To open **Settings**, right-click the notification area icon.

## Add text

1. Choose the **T** tool, then click inside the selected area.
2. Type your text. `Enter` starts a new line, `Ctrl+V` pastes, `Ctrl+Z` undoes typing, and `Ctrl+Y` restores it.
3. Click somewhere else to finish the note. `Enter` copies the image only after the note is finished.

`Esc` discards the note you are typing. Text does not wrap by itself, and a finished note cannot be edited. To change one, undo it and type it again.

## Use it with games

ezCapScreen copies the screen from outside the game. It does not inject code into a game, hook it, or read its memory.

The tool opens a window over the game, and this has some effects:

- A game in exclusive fullscreen loses focus, so the screen can go black for a moment before the selection appears. Borderless mode avoids this.
- Exclusive fullscreen games that minimize themselves are restored when the selection closes. Some games do not return to exclusive mode after they lose focus. Rise of the Tomb Raider is one, and it behaves the same way after Alt+Tab.

Testing has covered single-player games only, in menus. **Anti-cheat rules differ between games. Whether a game's anti-cheat accepts this tool is not known, so use it with online games at your own risk.**

## Build from source

Run `build.cmd`. It uses the C# compiler included with Windows and needs no other tools. The script writes `ezCapScreen.exe` to the top folder and `verify.exe` to the `tests` folder.

Exit ezCapScreen before you build, because Windows locks a running `.exe`.

To build the installer, install [Inno Setup 6](https://jrsoftware.org/isinfo.php) and run `build-installer.cmd`. The script writes `ezCapScreen-Setup.exe` to the top folder.

## Run the tests

`tests\verify.exe` drives the real program end to end. Its source is `tests\verify.cs`.

```
tests\verify.exe [--only binding|capture|settings] [folder for screenshots]
```

Before you run it, exit ezCapScreen and save your work. During a run, the tool does the following:

- It covers all monitors with a test pattern and sends real key presses.
- It moves the mouse cursor briefly.
- It overwrites the clipboard.
- It changes the ezCapScreen settings in the registry, then restores them.

## License

The source code uses the [MIT License](LICENSE). The character artwork is not covered by it. See [NOTICE](NOTICE) for details.

---

## ภาษาไทย

ezCapScreen เป็นโปรแกรมแคปหน้าจอขนาดเล็กสำหรับ Windows กดปุ่มลัด ลากเลือกพื้นที่ วาดหรือพิมพ์ทับ แล้วคัดลอกหรือบันทึกภาพ

### ความสามารถ

- เลือกพื้นที่บนจอใดก็ได้หรือคร่อมหลายจอ แล้วย้ายหรือปรับขนาดได้
- วาดสี่เหลี่ยม วงรี ลูกศร และพู่กัน ได้ 39 สี 3 ขนาด
- พิมพ์ข้อความภาษาไทยหรืออังกฤษ
- คัดลอกลงคลิปบอร์ด หรือบันทึกเป็น PNG, JPEG หรือ BMP
- เปลี่ยนปุ่มลัด และตั้งให้เปิดอัตโนมัติตอนเข้า Windows
- สลับภาษาไทยและอังกฤษ
- ใช้ธีมสว่างหรือมืดตาม Windows และปรับตามสเกลของแต่ละจอ

### สิ่งที่ต้องมี

- Windows 10 รุ่น 1703 ขึ้นไป ตอนนี้ทดสอบแล้วเฉพาะบน Windows 11
- .NET Framework 4.x ซึ่งมาพร้อม Windows 10 และ Windows 11

### ติดตั้งและเปิดโปรแกรม

1. ดาวน์โหลด `ezCapScreen-Setup.exe` จาก[หน้า Releases](../../releases)
2. เปิดไฟล์แล้วทำตามขั้นตอน ไม่ต้องใช้สิทธิ์ผู้ดูแลระบบ
3. เปิด ezCapScreen จากเมนู Start แล้วไอคอนจะขึ้นที่ถาดระบบบนแถบงาน

ถ้าต้องการลบโปรแกรม ให้เปิด **Settings > Apps > Installed apps** ของ Windows แล้วถอนการติดตั้ง ezCapScreen การตั้งค่าของโปรแกรมจะถูกลบไปด้วย

ถ้าไม่ต้องการติดตั้ง ให้ดาวน์โหลด `ezCapScreen.exe` จากหน้าเดียวกันแล้วเปิดใช้ได้เลย

ไฟล์เหล่านี้ไม่มีลายเซ็นดิจิทัล Windows SmartScreen จึงอาจแสดงคำเตือน ถ้าต้องการ คุณคอมไพล์เองจากซอร์สได้โดยรัน `build.cmd`

Microsoft Defender เคยตรวจจับรุ่น 1.0.0 ว่าเป็น `Behavior:Win32/Persistence.A!ml` สาเหตุน่าจะมาจากรุ่นนั้นเขียนค่าเปิดอัตโนมัติซ้ำทุกครั้งที่เปิดโปรแกรม รุ่น 1.0.1 จะเขียนค่านี้เฉพาะตอนเปลี่ยนตัวเลือก หรือตอนเลือกระหว่างติดตั้ง แต่ Defender ก็ยังอาจเตือนในจังหวะนั้นได้

### แคปหน้าจอ

1. กด `Ctrl+Alt+A` หรือคลิกไอคอนที่ถาดระบบ
2. ลากเลือกพื้นที่ ถ้าต้องการเลือกใหม่ ให้ลากตรงที่อื่นนอกกรอบ
3. ถ้าต้องการวาดทับ ให้เลือกเครื่องมือบนแถบเครื่องมือ แล้วเลือกขนาดและสี กด `Ctrl+Z` เพื่อลบรายการล่าสุด และ `Ctrl+Y` เพื่อเรียกกลับ
4. เลือกวิธีจบงาน:
   - เครื่องหมายถูกหรือ `Enter` คัดลอกภาพลงคลิปบอร์ด
   - ไอคอนแผ่นดิสก์ บันทึกภาพเป็นไฟล์
   - เครื่องหมายกากบาทหรือ `Esc` ปิดโดยไม่บันทึก

ถ้าคลิกขวาที่ไอคอนถาดระบบ จะเปิดเมนูที่มี **ตั้งค่า** สำหรับเปลี่ยนปุ่มลัด ภาษา และการเปิดอัตโนมัติ

### ใช้กับเกม

ezCapScreen จับภาพจากหน้าจอภายนอกเกม ไม่ฉีดโค้ดเข้าเกม ไม่ hook และไม่อ่านหน่วยความจำของเกม

โปรแกรมต้องเปิดหน้าต่างทับเกม จึงมีผลดังนี้:

- เกมแบบ exclusive fullscreen จะเสียโฟกัส จออาจดำแป๊บหนึ่งก่อนหน้าเลือกพื้นที่จะขึ้น ถ้าใช้โหมด Borderless จะไม่มีอาการนี้
- เกมที่ย่อตัวเองจะถูกเรียกกลับเมื่อปิดหน้าเลือกพื้นที่ แต่บางเกมจะไม่กลับเข้าโหมด exclusive หลังเสียโฟกัส เช่น Rise of the Tomb Raider ซึ่งเป็นแบบเดียวกันเมื่อกด Alt+Tab

ที่ผ่านมาทดสอบเฉพาะเกมเล่นคนเดียวในหน้าเมนู **กฎของระบบกันโกงแต่ละเกมไม่เหมือนกัน และยังไม่ทราบว่าระบบกันโกงแต่ละเกมจะยอมรับโปรแกรมนี้หรือไม่ ถ้าใช้กับเกมออนไลน์ ความเสี่ยงเป็นของผู้ใช้เอง**

### สัญญาอนุญาต

ซอร์สโค้ดใช้ [MIT License](LICENSE) แต่ไม่ครอบคลุมภาพตัวละคร ดูรายละเอียดได้ที่ [NOTICE](NOTICE)
