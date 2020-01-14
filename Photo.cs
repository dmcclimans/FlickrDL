using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FlickrDL
{
    // Represent a photo.
    // Similar to FlickrNet.Photo, but
    // - only has fields that I use
    // - add properties:
    //   - PhotosetTitle
    //   - PhotosetId
    //
    // FlickNet.Photo is not sealed, so I could derive from it. But FlickrNet.Photo does not
    // define a copy constructor, so there is no easy way to create a constructor for Photo that
    // takes a FlickNet.Photo as a parameter.
    public class Photo
    {
        public Photo()
        {
            OriginalSortOrder = -1;
        }

        public Photo(FlickrNet.Photo p) : this()
        {
            Title = p.Title;
            Description = p.Description;
            PhotoId = p.PhotoId;
            Tags = p.Tags.ToList<string>();
            DateTaken = p.DateTaken;
            OwnerName = p.OwnerName;
            OriginalUrl = p.OriginalUrl;
        }

        public Photo(FlickrNet.Photo p, Photoset ps) : this(p)
        {
            if (ps != null)
            {
                PhotosetTitle = ps.Title;
                PhotosetId = ps.PhotosetId;
            }
         }

        public int OriginalSortOrder { get; set; }

        public string Title { get; set; }
        public string Description { get; set; }
        public string PhotoId { get; set; }
        public List<string> Tags { get; set; }
        public DateTime DateTaken { get; set; }
        public string OwnerName { get; set; }
        public string OriginalUrl { get; }
        public string Location { get; set; }

        public string PhotosetTitle { get; set; }
        public string PhotosetId { get; set; }

    }
}
