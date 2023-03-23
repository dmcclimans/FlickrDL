[FlickrDL](https://github.com/dmcclimans/FlickrDL)
==========

FlickrDL is a Windows application that can download photos with selected metadata from a
Flickr account.

## Contents
* [Features](#features)
* [Requirements](#requirements)
* [Installation](#installation)
* [Usage](#usage)
* [Authentication](#authentication)
* [Known Issues](#KnownIssues)
* [License](#license)

## Features
* Downloads photos with selected metadata from a Flickr account or from selected albums in
  an account.
* Saves the Title as the IPTC Title.
* Saves the Description as the IPTC Caption.
* Saves the Tags as IPTC Keywords.
* Sets the file modified date to the date the photo was taken.
* Automatically renames photos when required to avoid duplicate names.
* When downloading from albums, creates a separate folder for each album.

## Requirements
* Requires Windows 10 or later.

## Installation
* Go to the FlickrDL
  [latest release](https://github.com/dmcclimans/FlickrDL/releases/latest)
  page and download `FlickrDL_x.y.zip` (where x.y is the version number).

* There is no install program.
  Unzip the files into a folder, and run `FlickrDL.exe`.

## Usage
![Screenshot_Main](Screenshot_Main.png)

1. The **Login account** is the account that you will be logged in as when you perform the
search and download.
Select **Public** to search without logging in (which will retrieve only photos that are
visible to the public.

    To search as a logged in user, you must add a login account. This will require you to
    authenticate the account. See the [Authentication](#authentication) section below.

2. The **Download account** is the account to search and download.
You can add any account where you know the account name or email address.

3. Check the **Download all photos** checkbox to download all photos from the download
account.

    This only works with accounts that have fewer than 4000 photos. For accounts that have
    more than 4000 photos, you must break the search into smaller pieces using the **Filter
    by date** option, or download by album.

4. Click the **Get Albums** button to retrieve the list of albums for the download account.

5. The **Album list** shows the albums retrieved by the **Get Albums** button. Select
the albums that you wish to process. You can click on the column headers to sort the
albums by name or other property.

6. Use the **Filter by date** option to select a subset of photos that were taken during
the specified date range.

7. Specify the path of the **Output Folder**.

    If you download by album, FlickDL will create a subfolder for each album name under
    the **Output Folder**. Each subfolder must be empty or not exist.

    If you download all photos, all the photos will be stored in the **Output Folder**.
    The **Output Folder** must be empty or not exist.

8. Click the **Download** button to download the files.

## Authentication

To add a **Login account**, you must "Authenticate" the FlickrDL application with
that account. This process tells Flickr to allow FlickrDL to access the account.
You must be logged in to the account to be able to authenticate.

FlickrDL will make any changes to your account, since it requests only **read** access to
your account.

To authenticate:

1. In your browser, log into the Flickr account that you wish to add as your login
account.

2. In FlickrDL, click the **Add** button to add a login account.

3. You will see the Add Login Account dialog:

![Screenshot_AddLoginAccount](Screenshot_AddLoginAccount.png)

4. Click the Authenticate button.

5. Your browser will open a new window or tab displaying a Flick Page asking you to
authorize FlickrDL to access your account.

    Click **OK, I'll authorize it** button.

6. Flickr will display another page showing the 9-digit authorization code. Copy and paste
this code into the **Verifier code** text box in FlickrDL.

7. Click the **Complete** button.

8. Close the Add Login Account dialog.

<a name="KnownIssues"></a>
## Known Issues

1. **Flickr time-out errors**

    You may experience time-out or other communication errors when the program is
    downloading data from Flickr. The program will attempt to recover from these errors by
    retrying the commands, but this is not always successful. After 3 failed attempts the
    program will display an error message and stop downloading.

    About all you can do at this point is repeat the download of the albums that did not
    complete. Check your output folder to see what albums were downloaded, and check the
    number of photos in each folder vs the number of photos reported by FlickrDL. If the
    number of photos match you do not need to re-download that album. If they do not
    match, you should delete the folder and allow FlickrDL to re-download those photos.

2. **Settings file moved in version 3.0**

    In version 3.0 the file that contains the application settings was renamed and moved.
    If you upgrade from an earlier version you will lose your settings, which will require
    you to re-authenticate your Flickr accounts.

    To avoid losing you settings you can manually move your settings file. In versions
    before 3.0 the settings file was named ``FlickrDLSettings.xml`` and was
    located in the same folder as the exe file. To use this file with version 3.0 or
    later, rename the file to ``Settings.xml``, and move or copy it to
    ``C:\Users\<username>\AppData\Roaming\FlickDL\``.

3. **Does not download video files**

    FlickrDL does not download video files. For videos, it downloads the "cover"
    image, which is what displays when you browse to that video on Flickr.

    Unfortunately, Flickr does not provide a reliable method for accessing the original
    video file. If you need to download videos there are other tools such as
    [Bulkr](https://getbulkr.com/)
    that support this. However be careful when using other tools with images, because they
    are not always reliable about copying tags from Flickr to the downloaded photo.

## License
FlickrDL is licensed under the MIT license. You may use the FlickrDL application in any
way you like. You may copy, distribute and modify the FlickrDL software provided you
include the copyright notice and license in all copies of the software.

FlickrAlbumSort links to a library that is licensed under the Apache 2.0 License.

FlickrDL uses, but does not link to, the program exiftool.exe. ExifTool is licensed under
the GNU General Public License (GPL) or the Artistic License. You may use the software in
any way you like. You may copy and distribute the software. You may modify the exiftool
software as long as you make the source code available.

See the [License.txt](License.txt) file for additional information.

