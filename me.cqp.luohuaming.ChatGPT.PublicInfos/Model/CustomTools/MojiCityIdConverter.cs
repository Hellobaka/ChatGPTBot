using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.Model.CustomTools
{
    public class MojiCityId
    {
        public int CityId { get; set; }

        public string Name { get; set; }

        public string NameEn { get; set; }

        public string CityLevelName { get; set; }

        public string CityLevelNameEn { get; set; }

        public string ProvinceName { get; set; }

        public string ProvinceNameEn { get; set; }

        public string CountryName { get; set; }

        public string CountryNameEn { get; set; }

        public double Longitude { get; set; }

        public double Latitude { get; set; }

        public int CityType { get; set; }

        public override string ToString()
        {
            return $"CityId: {CityId}, Name: {Name}, NameEn: {NameEn}, CityLevelName: {CityLevelName}, CityLevelNameEn: {CityLevelNameEn}, ProvinceName: {ProvinceName}, ProvinceNameEn: {ProvinceNameEn}, CountryName: {CountryName}, CountryNameEn: {CountryNameEn}, Longitude: {Longitude}, Latitude: {Latitude}, CityType: {CityType}";
        }
    }

    public static class MojiCityIdConverter
    {
        private static List<MojiCityId> CityList { get; set; } = [];

        /// <summary>
        /// 通过城市名称获取 cityId，支持中文和英文，支持模糊匹配
        /// </summary>
        /// <param name="cityName">城市名称</param>
        /// <param name="count">召回数量</param>
        /// <returns></returns>
        public static MojiCityId[] GetCityIdByName(string cityName, int count = 5)
        {
            MainSave.CQLog?.Info("墨迹天气转换", $"参数: {cityName}");
            if (CityList.Count == 0)
            {
                CityList = FromCsv(File.ReadAllText(Path.Combine(MainSave.AppDirectory, "moji.csv")));
                MainSave.CQLog?.InfoSuccess("加载墨迹天气数据", $"加载了 {CityList.Count} 城市");
            }
            var city = CityList.Where(c => c.Name.Equals(cityName, StringComparison.OrdinalIgnoreCase)
                || c.NameEn.Equals(cityName, StringComparison.OrdinalIgnoreCase)
                || c.CityLevelName.Equals(cityName, StringComparison.OrdinalIgnoreCase)
                || c.CityLevelNameEn.Equals(cityName, StringComparison.OrdinalIgnoreCase)
                || c.ProvinceName.Equals(cityName, StringComparison.OrdinalIgnoreCase)
                || c.ProvinceNameEn.Equals(cityName, StringComparison.OrdinalIgnoreCase)
                || c.CountryName.Equals(cityName, StringComparison.OrdinalIgnoreCase)
                || c.CountryNameEn.Equals(cityName, StringComparison.OrdinalIgnoreCase)
            ).Concat(CityList.Where(c => c.Name.Contains(cityName)
                || c.Name.Contains(cityName)
                || c.NameEn.Contains(cityName)
                || c.CityLevelName.Contains(cityName)
                || c.CityLevelNameEn.Contains(cityName)
                || c.ProvinceName.Contains(cityName)
                || c.ProvinceNameEn.Contains(cityName)
                || c.CountryName.Contains(cityName)
                || c.CountryNameEn.Contains(cityName)
            )).Take(count * 2).ToArray();
            MainSave.CQLog?.Info("墨迹天气转换", $"{cityName} => {city?.Count()} 个");
            return city;
        }

        private static List<MojiCityId> FromCsv(string csvText)
        {
            var result = new List<MojiCityId>();
            var lines = csvText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2) return result;
            for (int i = 1; i < lines.Length; i++)
            {
                var fields = ParseCsvLine(lines[i]);
                if (fields.Count < 12) continue;
                result.Add(new MojiCityId
                {
                    CityId = int.TryParse(fields[0], out var cityId) ? cityId : 0,
                    Name = fields[1],
                    NameEn = fields[2],
                    CityLevelName = fields[3],
                    CityLevelNameEn = fields[4],
                    ProvinceName = fields[5],
                    ProvinceNameEn = fields[6],
                    CountryName = fields[7],
                    CountryNameEn = fields[8],
                    Longitude = double.TryParse(fields[9], out var lon) ? lon : 0,
                    Latitude = double.TryParse(fields[10], out var lat) ? lat : 0,
                    CityType = int.TryParse(fields[11], out var cityType) ? cityType : 0
                });
            }
            return result;
        }

        private static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '\"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }
                if (c == ',' && !inQuotes)
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }
            result.Add(sb.ToString());
            return result;
        }
    }

}
