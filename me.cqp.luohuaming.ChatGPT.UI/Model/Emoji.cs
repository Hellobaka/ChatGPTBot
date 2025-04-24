using me.cqp.luohuaming.ChatGPT.PublicInfos;
using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using me.cqp.luohuaming.ChatGPT.PublicInfos.DB;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace me.cqp.luohuaming.ChatGPT.UI.Model
{
    [AddINotifyPropertyChangedInterface]
    public class Emoji
    {
        public bool Checked { get; set; }

        public string ImageAbsoultePath { get; set; }

        public string FilePath { get; set; }

        public string Description { get; set; }

        public string Hash { get; set; }

        public int EmbeddingDimensions { get; set; }

        public int UseCount { get; set; }

        public double CosineSimilarity { get; set; }

        public DateTime AddTime { get; set; }

        public Picture Raw { get; set; }

        public bool Duplicated { get; set; }

        public bool Finished { get; set; }

        public bool Success { get; set; }

        public static Model.Emoji ParseFromPicture(Picture picture)
        {
            return new Model.Emoji
            {
                ImageAbsoultePath = Path.Combine(MainSave.ImageDirectory, picture.FilePath),
                FilePath = picture.FilePath,
                Description = picture.Description,
                Hash = picture.Hash,
                EmbeddingDimensions = picture.Embedding.Length,
                UseCount = picture.UseCount,
                CosineSimilarity = 0,
                AddTime = picture.AddTime,
                Raw = picture
            };
        }
    }
}