using System.Collections.Concurrent;
using System.Collections.Generic;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.Model.CustomTools
{
    public static class PictureContextManager
    {
        private static readonly ConcurrentDictionary<string, List<string>> _pendingPictures = new();

        public static void AddPicture(string identity, string hash)
        {
            _pendingPictures.AddOrUpdate(identity, [hash], (key, list) =>
            {
                list.Add(hash);
                return list;
            });
        }

        public static List<string> GetAndClearPictures(string identity)
        {
            if (_pendingPictures.TryRemove(identity, out var list))
            {
                return list;
            }

            return [];
        }
    }
}
