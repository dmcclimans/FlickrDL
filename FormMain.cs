using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using FlickrNet;


namespace FlickrDL
{
    public partial class FormMain : Form
    {
        private Settings Settings { get; set; }
        private FlickrManager FlickrManager { get; set; }

        private FlickrNet.PhotoSearchExtras SearchExtras =
            FlickrNet.PhotoSearchExtras.OriginalUrl |
            FlickrNet.PhotoSearchExtras.Description |
            FlickrNet.PhotoSearchExtras.OwnerName |
            FlickrNet.PhotoSearchExtras.Tags |
            FlickrNet.PhotoSearchExtras.DateTaken;

        // Error message returned from BG search methods. Empty if no error.
        private string BGErrorMessage { get; set; }

        // User corresponding to Settings.FlickrSearchAccountName. This is the user
        // that is being searched.
        private User SearchAccountUser;

        // The list of photosets (albums) returned by GetAlbums, and used during a search.
        private SortableBindingList<Photoset> PhotosetList { get; set; }

        // The list of photos to be downloaded.
        private SortableBindingList<Photo> PhotoList { get; set; } = new SortableBindingList<Photo>();

        private bool FormIsLoaded { get; set; } = false;

        // The number of times we will try some Flickr commands before giving up. This only applies to
        // commands that can take a long time.
        private const int FlickrMaxTries = 3;

        // Checkbox that is put in the header of the dgvPhotosets.
        private CheckBox cbHeader;

        public FormMain()
        {
            InitializeComponent();
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            Settings = Settings.Load();
            if (Settings.FormMainLocation.X != 0 ||
                  Settings.FormMainLocation.Y != 0)
            {
                this.Location = Settings.FormMainLocation;
            }
            if (Settings.FormMainSize.Height != 0 ||
                  Settings.FormMainSize.Width != 0)
            {
                this.Size = Settings.FormMainSize;
            }

            FlickrManager = new FlickrManager(Settings);

            // Bind the login account list.
            cbLoginAccount.DataSource = Settings.FlickrLoginAccountList;
            cbLoginAccount.DisplayMember = "CombinedName";
            if (Settings.FlickrLoginAccountName.Length > 0)
            {
                int index = cbLoginAccount.FindString(Settings.FlickrLoginAccountName);
                if (index >= 0)
                {
                    cbLoginAccount.SelectedIndex = index;
                }
            }

            cbSearchAccount.DataSource = Settings.FlickrSearchAccountList;
            cbSearchAccount.DisplayMember = "CombinedName";
            if (Settings.FlickrSearchAccountName.Length > 0)
            {
                int index = cbSearchAccount.FindString(Settings.FlickrSearchAccountName);
                if (index >= 0)
                {
                    cbSearchAccount.SelectedIndex = index;
                }
            }

            // For the Album (photoset) DataGridView, add a "select all" checkbox to the header row
            // Set checkbox header to center of header cell. This is kluge code from the internet,
            // modified slightly so it looks right on my system.
            Rectangle rect = dgvPhotosets.GetCellDisplayRectangle(0, -1, true);
            rect.X = rect.X + rect.Width / 4;
            rect.Y = rect.Y + 2;

            cbHeader = new CheckBox
            {
                Name = "cbHeader",
                Size = new System.Drawing.Size(18, 18),
                Location = rect.Location
            };
            cbHeader.CheckedChanged += new EventHandler(cbHeader_CheckedChanged);
            dgvPhotosets.Controls.Add(cbHeader);

            // set up bindings
            chkDownloadAllPhotos.DataBindings.Add("Checked", Settings, "DownloadAllPhotos", true, DataSourceUpdateMode.OnPropertyChanged);
            btnGetAlbums.DataBindings.Add("Enabled", Settings, "GetAlbumsButtonEnabled");
            btnDownload.DataBindings.Add("Enabled", Settings, "DownloadButtonEnabled");
            chkFilterDate.DataBindings.Add("Checked", Settings, "FilterByDate", true, DataSourceUpdateMode.OnPropertyChanged);
            dateTimePickerStart.DataBindings.Add("Value", Settings, "StartDate", true, DataSourceUpdateMode.OnPropertyChanged);
            dateTimePickerStart.DataBindings.Add("Enabled", Settings, "FilterDateEnabled");
            dateTimePickerStop.DataBindings.Add("Value", Settings, "StopDate", true, DataSourceUpdateMode.OnPropertyChanged);
            dateTimePickerStop.DataBindings.Add("Enabled", Settings, "FilterDateEnabled");
            txtOutputFolder.DataBindings.Add("Text", Settings, "OutputFolder");

            FormIsLoaded = true;
        }

        private void cbHeader_CheckedChanged(object sender, EventArgs e)
        {
            bool enable = ((CheckBox)dgvPhotosets.Controls.Find("cbHeader", true)[0]).Checked;
            for (int i = 0; i < dgvPhotosets.RowCount; i++)
            {
                dgvPhotosets[0, i].Value = enable;
            }
            dgvPhotosets.EndEdit();
        }

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Save the form location
            Settings.FormMainLocation = this.Location;
            Settings.FormMainSize = this.Size;

            Settings.Save();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void btnDownload_Click(object sender, EventArgs e)
        {
            BGErrorMessage = "";

            SearchAccountUser = (User)cbSearchAccount.SelectedItem;
            if (SearchAccountUser == null)
            {
                MessageBox.Show("No search account selected");
                return;
            }

            Stopwatch RunTimer = Stopwatch.StartNew();

            bool searchSuccessful = false;
            if (Settings.DownloadAllPhotos)
            {
                searchSuccessful = DownloadAllPhotos();
            }
            else
            {
                searchSuccessful = DownloadPhotosets();
            }

            RunTimer.Stop();
            if (searchSuccessful)
            {
                if (String.IsNullOrWhiteSpace(BGErrorMessage))
                {
                    MessageBox.Show(String.Format("Downloaded {0} photos in {1}:{2:mm}:{2:ss}.",
                        PhotoList.Count.ToString(),
                        (int)RunTimer.Elapsed.TotalHours, RunTimer.Elapsed, RunTimer.Elapsed));
                }
                else if (BGErrorMessage.Contains("Too many photos"))
                {
                    int index = BGErrorMessage.IndexOf(":");
                    int count = 0;
                    if (index >= 0)
                    {
                        int.TryParse(BGErrorMessage.Substring(index + 1), out count);
                    }
                    if (Settings.DownloadAllPhotos)
                    {
                        MessageBox.Show("Too many photos found.\r\n\r\n" +
                            "Flickr limits the number of photos returned from a search to about 4000. " +
                            $"This search found {count} photos and the resulting photo list is not accurate.\r\n\r\n" +
                            "Reduce the size of the search by either searching by album or limiting the search by date.");
                    }
                    else
                    {
                        // It is not clear from the FlickrApi documentation whether the 4000 photo limit applies
                        // when searching albums (Photosets.GetPhotos).
                        // At present I assume it does not. This seems consistent with the fact that you cannot
                        // filter a Photoset.GetPhotos call by date.
                        // This error message is not currently returned by my code when searching albums, so
                        // you will never see it. But there is some disabled (ifdef) code in BGDownloadPhotosets
                        // that could be enabled to return this error message.
                        MessageBox.Show("Too many photos found.\r\n\r\n" +
                            "Flickr limits the number of photos returned from a search to about 4000. " +
                            $"One of the album searches found {count} photos and the resulting photo list is not accurate.\r\n\r\n" +
                            "Reduce the size of the search by reducing the number of photos in the albums.");
                    }
                }
                else
                {
                    MessageBox.Show(BGErrorMessage);
                }
            }
        }

        private bool IsDirectoryEmpty(string folderPpath)
        {
            if (!Directory.Exists(folderPpath))
                return true;
            return !Directory.EnumerateFileSystemEntries(folderPpath).Any();   
        }

        private bool CheckFolderEmpty(string folderPath)
        {
            try
            {
                if (String.IsNullOrWhiteSpace(folderPath))
                {
                    MessageBox.Show("No output folder specified");
                    return false;
                }
                if (!IsDirectoryEmpty(folderPath))
                {
                    DialogResult result = MessageBox.Show(
                        "The folder \"" + Path.GetFileName(folderPath) + "\" exists and is not empty.",
                        "FlickrDL", MessageBoxButtons.OK);
                    return false;
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
                return false;
            }
            return true;
        }

        private bool DownloadAllPhotos()
        {
            if (!CheckFolderEmpty(Settings.OutputFolder))
                return false;

            FormProgress dlg = new FormProgress("Download all photos", BGDownloadAllPhotos);

            // Show dialog with Synchronous/blocking call.
            // BGDownloadAllPhotos() is called by dialog.
            DialogResult result = dlg.ShowDialog();
            return result == DialogResult.OK;
        }

        private bool DownloadPhotosets()
        {
            // Check for existing photoset subfolders (with contents)
            int enabledCount = 0;
            if (PhotosetList != null)
            {
                foreach (Photoset ps in PhotosetList)
                {
                    if (ps.EnableSearch)
                    {
                        enabledCount++;
                        string folderPath = Path.Combine(Settings.OutputFolder, ps.Title);
                        if (!CheckFolderEmpty(folderPath))
                            return false;
                    }
                }
            }
            // Check for no photosets enabled
            if (enabledCount == 0)
            {
                MessageBox.Show("No albums enabled to search");
                return false;
            }

            FormProgress dlg = new FormProgress("Download albums", BGDownloadPhotosets);

            // Show dialog with Synchronous/blocking call.
            // BGDownloadPhotosets() is called by dialog.
            DialogResult result = dlg.ShowDialog();
            return result == DialogResult.OK;
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FormAbout dlg = new FormAbout(Settings);
            dlg.ShowDialog(this);
        }

        private void btnGetAlbums_Click(object sender, EventArgs e)
        {
            BGErrorMessage = "";

            dgvPhotosets.Rows.Clear();

            SearchAccountUser = (User)cbSearchAccount.SelectedItem;
            if (SearchAccountUser == null)
            {
                MessageBox.Show("No search account selected");
                return;
            }
            PhotosetList = new SortableBindingList<Photoset>();

            FormProgress dlg = new FormProgress("Find albums", BGFindPhotosets);

            // Show dialog with Synchronous/blocking call.
            // BGFindPhotosets() is called by dialog.
            DialogResult result = dlg.ShowDialog();
            if (result == DialogResult.OK)
            {
                if (!String.IsNullOrWhiteSpace(BGErrorMessage))
                {
                    MessageBox.Show(BGErrorMessage);
                }
                else
                {
                    bindingSourcePhotosets.DataSource = PhotosetList;
                    if (dgvPhotosets.Rows.Count > 0 &&
                        dgvPhotosets.Rows[0].Cells.Count > 0)
                    {
                        dgvPhotosets.CurrentCell = dgvPhotosets.Rows[0].Cells[0];
                        dgvPhotosets.Rows[0].Selected = true;
                    }
                }
            }
            else
            {
                // Search was canceled. Do nothing.
            }
        }


        private void BGFindPhotosets(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;

            FlickrNet.Flickr f = FlickrManager.GetFlickrAuthInstance();
            if (f == null)
            {
                BGErrorMessage = "You must authenticate before you can download data from Flickr.";
                return;
            }

            try
            {
                int page = 1;
                int perPage = 500;
                FlickrNet.PhotosetCollection photoSets = new FlickrNet.PhotosetCollection();
                FlickrNet.PhotoSearchExtras PhotosetExtras = 0;
                do
                {
                    bool success = false;
                    for (int attempt = 0; attempt < FlickrMaxTries && !success; attempt++)
                    {
                        try
                        {
                            photoSets = f.PhotosetsGetList(SearchAccountUser.UserId, page, perPage, PhotosetExtras);
                            success = true;
                        }
                        catch (FlickrNet.FlickrException ex)
                        {
                            // Save the *first* error message for display, not subsequent ones.
                            if (attempt == 0)
                                BGErrorMessage = "Album search failed. Flickr error: " + ex.Message;
                        }
                        catch (Exception ex)
                        {
                            if (attempt == 0)
                                BGErrorMessage = "Album search failed. Unexpected Flickr error: " + ex.Message;
                        }
                    }
                    if (!success)
                    {
                        return;
                    }
                    BGErrorMessage = "";

                    foreach (FlickrNet.Photoset ps in photoSets)
                    {
                        PhotosetList.Add(new Photoset(ps));
                        int index = PhotosetList.Count - 1;
                        PhotosetList[index].OriginalSortOrder = index;
                    }
                    page = photoSets.Page + 1;

                }
                while (page <= photoSets.Pages);
            }
            catch (FlickrNet.FlickrException ex)
            {
                BGErrorMessage = "Album search failed. Flickr error: " + ex.Message;
                return;
            }
            catch (Exception ex)
            {
                BGErrorMessage = "Album search failed. Unexpected error: " + ex.Message;
                return;
            }
        }

        // Background thread to download for photos
        private void BGDownloadAllPhotos(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = (BackgroundWorker)sender;

            worker.ReportProgress(0, "Searching all photos");

            PhotoList = new SortableBindingList<Photo>();

            FlickrNet.Flickr f = FlickrManager.GetFlickrAuthInstance();
            if (f == null)
            {
                BGErrorMessage = "You must authenticate before you can download data from Flickr.";
                return;
            }

            try
            {
                FlickrNet.PhotoSearchOptions options = new FlickrNet.PhotoSearchOptions();
                options.Extras = SearchExtras;
                options.SortOrder = FlickrNet.PhotoSearchSortOrder.DateTakenAscending;
                if (Settings.FilterByDate)
                {
                    options.MinTakenDate = Settings.StartDate.Date;
                    options.MaxTakenDate = Settings.StopDate.Date + new TimeSpan(23, 59, 59);
                }
                options.UserId = SearchAccountUser.UserId;
                options.Page = 1;
                options.PerPage = 500;

                FlickrNet.PhotoCollection photoCollection = null;
                do
                {
                    if (worker.CancellationPending) // See if cancel button was pressed.
                    {
                        return;
                    }

                    // Try searching Flickr up to FlickrMaxTries times
                    bool success = false;
                    for (int attempt = 0; attempt < FlickrMaxTries && !success; attempt++)
                    {
                        try
                        {
                            photoCollection = f.PhotosSearch(options);
                            success = true;
                        }
                        catch (FlickrNet.FlickrException ex)
                        {
                            // Save the *first* error message for display, not subsequent ones.
                            if (attempt == 0)
                                BGErrorMessage = "Search failed. Flickr error: " + ex.Message;
                        }
                        catch (Exception ex)
                        {
                            if (attempt == 0)
                                BGErrorMessage = "Search failed. Unexpected Flickr error: " + ex.Message;
                        }
                    }
                    if (!success)
                    {
                        return;
                    }
                    BGErrorMessage = "";

                    if (photoCollection != null && photoCollection.Total > 3999)
                    {
                        BGErrorMessage = $"Too many photos: {photoCollection.Total}";
                        return;
                    }
                    foreach (FlickrNet.Photo flickrPhoto in photoCollection)
                    {
                        AddPhotoToList(f, flickrPhoto, null);
                    }
                    // Calculate percent complete based on how many pages we have completed.
                    int percent = (options.Page * 100 / photoCollection.Pages);
                    worker.ReportProgress(percent, "Searching all photos");

                    options.Page = photoCollection.Page + 1;
                }
                while (options.Page <= photoCollection.Pages);

            }
            catch (FlickrNet.FlickrException ex)
            {
                BGErrorMessage = "Search failed. Flickr error: " + ex.Message;
                return;
            }
            catch (Exception ex)
            {
                BGErrorMessage = "Search failed. Unexpected Flickr error: " + ex.Message;
                return;
            }

            DownloadFiles(worker, e);
        }

        // Background worker task to search selected photosets.
        private void BGDownloadPhotosets(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = (BackgroundWorker)sender;

            worker.ReportProgress(0, "Connecting");

            PhotoList = new SortableBindingList<Photo>();

            // Count the number of enabled photosets, so we can do an estimate of percent complete;
            int enabledPhotosets = 0;
            foreach (Photoset photoset in PhotosetList)
            {
                if (photoset.EnableSearch)
                {
                    enabledPhotosets++;
                }
            }

            if (enabledPhotosets == 0)
            {
                // No photosets are enabled. We are done.
                return;
            }

            try
            {
                int indexPhotoset = 0;

                FlickrNet.Flickr f = FlickrManager.GetFlickrAuthInstance();
                if (f == null)
                {
                    BGErrorMessage = "You must authenticate before you can download data from Flickr.";
                    return;
                }

                // Iterate over the photosets and get the photos from each set.
                FlickrNet.PhotosetPhotoCollection photoCollection = new FlickrNet.PhotosetPhotoCollection();

                foreach (Photoset photoset in PhotosetList)
                {
                    if (worker.CancellationPending)
                    {
                        e.Cancel = true;
                        return;
                    }

                    if (photoset.EnableSearch)
                    {
                        int percent = indexPhotoset * 100 / enabledPhotosets;
                        string description = "Searching Album " + photoset.Title;
                        worker.ReportProgress(percent, description);

                        int page = 1;
                        int perpage = 500;
                        do
                        {
                            if (worker.CancellationPending) // See if cancel button was pressed.
                            {
                                e.Cancel = true;
                                return;
                            }

                            // Try searching Flickr up to FlickrMaxTries times
                            bool success = false;
                            for (int attempt = 0; attempt < FlickrMaxTries && !success; attempt++)
                            {
                                try
                                {
                                    photoCollection = f.PhotosetsGetPhotos(photoset.PhotosetId, SearchExtras, page, perpage);
                                    success = true;
                                }
                                catch (FlickrNet.FlickrException ex)
                                {
                                    // Save the *first* error message for display, not subsequent ones.
                                    if (attempt == 0)
                                        BGErrorMessage = "Search failed. Flickr error: " + ex.Message;
                                }
                                catch (Exception ex)
                                {
                                    // Save the *first* error message for display, not subsequent ones.
                                    if (attempt == 0)
                                        BGErrorMessage = "Search failed. Unexpected Flickr error: " + ex.Message;
                                }
                            }
                            if (!success)
                            {
                                return;
                            }
                            BGErrorMessage = "";

#if false
                            // It is not clear from the documentation whether the limit of 4000 photos per search applies
                            // to album searches. If an album has more than 4000 photos, is the result of GetPhotos
                            // accurate? I'm going to assume for now that it is. If not, you can enable this code.
                            if (photoCollection.Total > 3999)
                            {
                                BGErrorMessage = $"Too many photos: {photoCollection.Total}";
                                return;
                            }
#endif
                            foreach (FlickrNet.Photo flickrPhoto in photoCollection)
                            {
                                if (!AddPhotoToList(f, flickrPhoto, photoset))
                                {
                                    return;
                                }
                            }
                            // Calculate percent complete based on both how many photo sets we have completed,
                            // plus how many pages we have read
                            percent = (indexPhotoset * 100 + page * 100 / photoCollection.Pages) / enabledPhotosets;
                            worker.ReportProgress(percent, description);
                            page = photoCollection.Page + 1;
                        }
                        while (page <= photoCollection.Pages);

                        indexPhotoset++;
                    }
                }
            }
            catch (FlickrNet.FlickrException ex)
            {
                BGErrorMessage = "Search failed. Flickr error: " + ex.Message;
                return;
            }
            catch (Exception ex)
            {
                BGErrorMessage = "Search failed. Unexpected error: " + ex.Message;
                return;
            }

            DownloadFiles(sender, e);
        }

        private bool AddPhotoToList(FlickrNet.Flickr f, FlickrNet.Photo flickrPhoto, Photoset photoset)
        {
            // Filter by date, if filter option enabled and date taken is known.
            if (!Settings.FilterByDate ||
                flickrPhoto.DateTakenUnknown ||
                (flickrPhoto.DateTaken.Date >= Settings.StartDate && flickrPhoto.DateTaken.Date <= Settings.StopDate))
            {
                Photo photo = new Photo(flickrPhoto, photoset);
                PhotoList.Add(photo);

                FlickrNet.PhotoInfo info = null;
                    
                // Get the photo info to get the raw tags, and put them into the photo object.
                // The raw tags are as uploaded or entered -- with spaces, punctuation, and
                // upper/lower case.
                // Try download from Flickr up to FlickrMaxTries times
                bool success = false;
                for (int attempt = 0; attempt < FlickrMaxTries && !success; attempt++)
                {
                    try
                    {
                        info = f.PhotosGetInfo(flickrPhoto.PhotoId);
                        success = true;
                    }
                    catch (FlickrNet.FlickrException ex)
                    {
                        // Save the *first* error message for display, not subsequent ones.
                        if (attempt == 0)
                            BGErrorMessage = "Getting tags failed. Flickr error: " + ex.Message;
                    }
                    catch (Exception ex)
                    {
                        if (attempt == 0)
                            BGErrorMessage = "Getting tags failed. Unexpected Flickr error: " + ex.Message;
                    }
                }
                if (!success || info == null)
                {
                    return false;
                }
                BGErrorMessage = "";
                
                photo.Tags.Clear();
                for (int i=0; i<info.Tags.Count; i++)
                {
                    photo.Tags.Add(info.Tags[i].Raw);
                }
            }
            return true;
        }

        private void DownloadFiles(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = (BackgroundWorker)sender;

            // Iterate over the photos.
            // Download them and apply tags.
            try
            {
                for (int photoIndex = 0; photoIndex < PhotoList.Count; photoIndex++)
                {
                    if (worker.CancellationPending) // See if cancel button was pressed.
                    {
                        e.Cancel = true;
                        return;
                    }

                    Photo photo = PhotoList[photoIndex];

                    string filePath = GetDestinationFilePath(photo);

                    int percent = photoIndex * 100 / PhotoList.Count;
                    string description = "Downloading " + photo.Title;
                    worker.ReportProgress(percent, description);

                    // Try download from Flickr up to FlickrMaxTries times
                    bool success = false;
                    for (int attempt = 0; attempt < FlickrMaxTries && !success; attempt++)
                    {
                        try
                        {
                            // Download the image file.
                            using (WebClient client = new WebClient())
                            {
                                client.DownloadFile(photo.OriginalUrl, filePath);
                            }
                            success = true;
                        }
                        catch (Exception ex)
                        {
                            if (attempt == 0)
                                BGErrorMessage = "Download failed. Error: " + ex.Message;
                        }
                    }
                    if (!success)
                    {
                        return;
                    }
                    BGErrorMessage = "";


                    // Create a json file with the title, description, and keywords (Flickr tags).
                    string jsonFilePath = Path.ChangeExtension(filePath, ".json");
                    jsonFilePath = GetUniqueFilename(jsonFilePath);
                    WriteJsonFile(photo, filePath, jsonFilePath);

                    // Use ExifTools to update the title, description, and keywords of the photo file.
                    ApplyExifData(filePath, jsonFilePath);

                    // Delete the json file.
                    File.Delete(jsonFilePath);
                }
            }
            catch (Exception ex)
            {
                BGErrorMessage = "Download failed. Unexpected error: " + ex.Message;
                return;
            }
        }

        private void WriteJsonFile(Photo photo, string filePath, string jsonFilePath)
        {
            // Create a json file with the title, description, and keywords (Flickr tags).
            // Note the SourceFile entry *must* use forward slashes, not back slashes.
             string jsonText = "[{" +
                Environment.NewLine +
                "  \"SourceFile\": \"" + filePath.Replace('\\', '/') + "\"," +
                Environment.NewLine +
                "  \"ObjectName\": \"" + photo.Title + "\"," +
                Environment.NewLine +
                "  \"ImageDescription\": \"" + photo.Description + "\"," +
                //Environment.NewLine +
                //"  \"Comments\": \"" + photo.Comment + "\"," +
                Environment.NewLine +
                "  \"Keywords\": [";
            for (int i = 0; i < photo.Tags.Count; i++)
            {
                if (i > 0)
                {
                    jsonText += ",";
                }
                jsonText += "\"" + photo.Tags[i] + "\"";
            }
            jsonText += "]" +
                Environment.NewLine +
                "}]";

            File.WriteAllText(jsonFilePath, jsonText);
        }


        private void ApplyExifData(string filePath, string jsonFilePath)
        {
            // Run exiftool
            // -q -q: suppress info messages and warnings
            // -overwrite_original: overwrite the original file
            // -FileModifyDate<DateTimeOriginal: set the file modify date to the DateTimeOriginal
            //      from the exif information.
            // -j="jsonFilePath": Apply the tags from the json file.
            // filePath: The image file to process.
            ExecuteCommand cmd = new ExecuteCommand();
            string cmdString =
                "exiftool -q -q -overwrite_original \"-FileModifyDate<DateTimeOriginal\"" +
                " -j=\"" + jsonFilePath + "\"" +
                " \"" + filePath + "\""
                ;
            string s = cmd.Execute(cmdString);
            if (!string.IsNullOrWhiteSpace(s))
            {
                throw new Exception(s);
            }
        }

         private string QuoteString(string s)
        {
            return "\"" + s + "\"";
        }

        // Create the full path to the destination file.
        // Will create folder if necessary
        // Will append _n (where n is a number) to the file name if needed to get
        // a file name that does not already exist.
        private string GetDestinationFilePath(Photo photo)
        {
            string filePath = Settings.OutputFolder;
            if (!Settings.DownloadAllPhotos)
            {
                filePath = Path.Combine(filePath, photo.PhotosetTitle);
            }
            // Make sure the folder exists, create it and all parent folders as necessary.
            Directory.CreateDirectory(filePath);

            // Append the file name
            filePath = Path.Combine(filePath, photo.Title);

            // Append the extension
            string ext = Path.GetExtension(photo.OriginalUrl);
            filePath = Path.ChangeExtension(filePath, ext);

            filePath = GetUniqueFilename(filePath);

            return filePath;
        }

        private string GetUniqueFilename(string filePath)
        {
            string ext = Path.GetExtension(filePath);
            string basePath = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath));
            string newFilePath = filePath;
            int i = 0;
            while (File.Exists(newFilePath))
            {
                i++;
                newFilePath = Path.ChangeExtension(basePath + "_" + i.ToString(), ext);
                if (i>20)
                {
                    throw new Exception("Too many files with same name: " + basePath);
                }
            }
            return newFilePath;
        }

        private void btnRemoveLoginAccount_Click(object sender, EventArgs e)
        {
            var user = (User)cbLoginAccount.SelectedItem;
            if (user == null)
            {
                MessageBox.Show("No user selected.");
            }
            else if (user.UserName == "Public")
            {
                MessageBox.Show("Cannot remove access to public photos.");
            }
            else
            {
                DialogResult result = MessageBox.Show("Remove the login account '" + user.CombinedName + "'?",
                    "FlickrDL", MessageBoxButtons.OKCancel);
                if (result == DialogResult.OK)
                {
                    Settings.RemoveFlickrLoginAccountName(user);
                }
            }
        }

        private void btnAddLoginAccount_Click(object sender, EventArgs e)
        {
            FormAddLoginAccount dlg = new FormAddLoginAccount(Settings, FlickrManager);
            DialogResult result = dlg.ShowDialog();
            if (result == DialogResult.OK)
            {
                User NewUser = dlg.NewUser;
                if (NewUser != null)
                {
                    try
                    {
                        cbLoginAccount.SelectedItem = NewUser;
                        Settings.FlickrLoginAccountName = NewUser.UserName;
                        cbSearchAccount.SelectedItem = NewUser;
                        Settings.FlickrSearchAccountName = NewUser.UserName;
                    }
                    catch (Exception)
                    {
                        // Ignore error.
                    }
                }
            }
        }

        private void cbLoginAccount_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (FormIsLoaded)
            {
                User user = (User)cbLoginAccount.SelectedItem;
                if (user != null)
                {
                    Settings.FlickrLoginAccountName = user.UserName;
                }
            }
        }

        private void btnAddSearchAccount_Click(object sender, EventArgs e)
        {
            FormAddSearchAccount dlg = new FormAddSearchAccount(Settings, FlickrManager);
            DialogResult result = dlg.ShowDialog();
            if (result == DialogResult.OK)
            {
                User NewUser = dlg.NewUser;
                if (NewUser != null)
                {
                    try
                    {
                        cbSearchAccount.SelectedItem = NewUser;
                        Settings.FlickrSearchAccountName = NewUser.UserName;
                    }
                    catch (Exception)
                    {
                        // Ignore error.
                    }
                }
            }
        }

        private void btnRemoveSearchAccount_Click(object sender, EventArgs e)
        {
            User user = (User)cbSearchAccount.SelectedItem;
            if (user == null)
            {
                MessageBox.Show("No user selected.");
            }
            else
            {
                DialogResult result = MessageBox.Show("Remove the search account '" + user.CombinedName + "'?",
                "FlickrDL", MessageBoxButtons.OKCancel);
                if (result == DialogResult.OK)
                {
                    Settings.RemoveFlickrSearchAccountName(user);
                }
            }
        }

        private void cbSearchAccount_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (FormIsLoaded)
            {
                User user = (User)cbSearchAccount.SelectedItem;
                if (user != null)
                {
                    Settings.FlickrSearchAccountName = user.UserName;
                }
            }
        }

        private void chkDownloadAllPhotos_CheckedChanged(object sender, EventArgs e)
        {
            if (FormIsLoaded)
            {
                if (chkDownloadAllPhotos.Checked)
                {
                    dgvPhotosets.Rows.Clear();
                    cbHeader.Checked = false;
                }
            }
        }

        private void btnBrowseOutputFolder_Click(object sender, EventArgs e)
        {
            string path = Settings.OutputFolder;
            if (!string.IsNullOrEmpty(path))
            {
                // Sometimes the existing output folder has been deleted. Go up one level to see
                // if that is there. If so, use it for the initial directory.
                // If the initial directory doesn't exist, Windows will start from Documents.
                if (!Directory.Exists(path)) 
                {
                    // Remove any trailing backslash
                    path = path.TrimEnd(new[] { '/', '\\' });
                    DirectoryInfo parentDir = Directory.GetParent(path);
                    if (parentDir != null) 
                    {
                        path = parentDir.FullName;
                    }
                }
            }

            FolderSelectDialog dlg = new FolderSelectDialog()
            {
                Title = "Select output folder"
            };
            if (!string.IsNullOrEmpty(path))
            {
                dlg.InitialDirectory = path;
            }
            
            if (dlg.Show(Handle))
            {
                Settings.OutputFolder = dlg.FileName;
            }
        }
    }
}
