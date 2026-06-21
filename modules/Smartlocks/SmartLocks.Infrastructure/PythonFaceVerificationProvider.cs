using System.Net.Http.Json;
using FaceVerification.Domain;

namespace SmartLocks.Infrastructure;

public class PythonFaceVerificationProvider : IFaceVerificationProvider
{
    private readonly HttpClient _httpClient;

    public PythonFaceVerificationProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> VerifyAsync(Guid userId, Stream imageStream, CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        var streamContent = new StreamContent(imageStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        form.Add(streamContent, "image", "face.jpg");
        form.Add(new StringContent(userId.ToString()), "userId");

        var response = await _httpClient.PostAsync("/verify", form, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<FaceVerifyResponse>(cancellationToken: cancellationToken);
        return result?.Match ?? false;
    }

    public async Task EnrollAsync(Guid userId, List<Stream> imageStreams, CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(userId.ToString()), "userId");

        int i = 0;
        foreach (var stream in imageStreams)
        {
            var streamContent = new StreamContent(stream);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            form.Add(streamContent, "images", $"face_{i++}.jpg");
        }

        var response = await _httpClient.PostAsync("/enroll", form, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private class FaceVerifyResponse
    {
        public bool Match { get; set; }
        public float Confidence { get; set; }
    }
}
