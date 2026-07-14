using Microsoft.Extensions.Options;
using Services._Price._RZPriceService.DTOs;
using System.Net.Http.Json;

namespace Services._Price._RZPriceService
{
    public class RZPriceService : IRZPriceService
    {
        private readonly HttpClient _httpClient;
        private readonly RZPriceSetting _setting;

        public RZPriceService(HttpClient httpClient, IOptions<RZPriceSetting> options)
        {
            _httpClient = httpClient;
            _setting = options.Value;
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, string url)
        {
            var request = new HttpRequestMessage(method, url);

            request.Headers.Add("ApplicationId", "test");
            request.Headers.Add("Signature", _setting.MasterKey);

            return request;
        }

        public async Task<RZPriceResult?> FetchTokenPriceAsync(string tokenName)
        {
            try
            {
                var request = CreateRequest(
               HttpMethod.Get,
               $"{_setting.BaseUrl}/Price/FetchTokenPrice?tokenName={Uri.EscapeDataString(tokenName)}");

                var response = await _httpClient.SendAsync(request);

                response.EnsureSuccessStatusCode();

                return await response.Content.ReadFromJsonAsync<RZPriceResult>();
            }
            catch (Exception)
            {
                return null;
            }
           
        }

        public async Task<List<RZPriceResult>> FetchTokensPriceAsync(List<string> tokenNames)
        {
            try
            {
                var request = CreateRequest(
               HttpMethod.Post,
               $"{_setting.BaseUrl}/Price/FetchTokensPrice");

                request.Content = JsonContent.Create(tokenNames);

                var response = await _httpClient.SendAsync(request);

                response.EnsureSuccessStatusCode();

                return await response.Content.ReadFromJsonAsync<List<RZPriceResult>>()
                       ?? new List<RZPriceResult>();
            }
            catch (Exception)
            {

                return null;
            }

        }

        public async Task<CoinHistoryData?> FetchTokenHistoryAsync(string tokenName)
        {

            try
            {
                var request = CreateRequest(
              HttpMethod.Get,
              $"{_setting.BaseUrl}/Price/FetchTokenHistory?tokenName={Uri.EscapeDataString(tokenName)}");

                var response = await _httpClient.SendAsync(request);

                response.EnsureSuccessStatusCode();

                return await response.Content.ReadFromJsonAsync<CoinHistoryData>();
            }
            catch (Exception ex)
            {
                return null;
            }
          
        }
    }
}
