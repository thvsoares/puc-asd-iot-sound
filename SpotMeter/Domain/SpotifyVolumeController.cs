using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

namespace SpotMeter.Domain
{
    public class SpotifyVolumeController
    {
        /// <summary>
        /// OAuth Token with the scope "user-modify-playback-state"
        /// Can be generated on https://developer.spotify.com/web-api/console/put-volume/
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// Spotify player volume endpoint
        /// </summary>
        private static string VOLUME_ENDPOINT = "https://api.spotify.com/v1/me/player/volume?volume_percent={0}";

        /// <summary>
        /// OAuth authorization header
        /// </summary>
        private static string AUTHORIZATION_HEADER = "Bearer {0}";

        /// <summary>
        /// Set the volume percent of the Spotify player
        /// </summary>
        /// <param name="volume">Volume percent</param>
        /// <exception cref="ArgumentOutOfRangeException">If the volumePercent is greater than 100</exception>
        public async Task SetVolume(byte volumePercent)
        {
            if (volumePercent > 100)
                throw new ArgumentOutOfRangeException();

            using (var client = new HttpClient())
            {
                var uri = new Uri(string.Format(VOLUME_ENDPOINT, volumePercent));
                client.DefaultRequestHeaders.Add("Authorization", string.Format(AUTHORIZATION_HEADER, Token));
                var response = await client.PutAsync(uri, null);

                Debug.WriteLine(response);
            }
        }
    }
}
