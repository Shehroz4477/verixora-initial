using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Domain;
using FaceVerification.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace SmartLocks.Infrastructure;

public class PythonFaceVerificationProvider : IFaceVerificationProvider
{
    private const int MinimumEnrollmentFrames = 3;
    private const int MaximumEnrollmentFrames = 5;
    private const int EmbeddingDimensions = 128;
    private readonly HttpClient _httpClient;
    private readonly IFaceTemplateRepository _templateRepository;
    private readonly IFaceTemplateProtector _templateProtector;
    private readonly bool _allowPassiveDevelopmentVerification;

    public PythonFaceVerificationProvider(
        HttpClient httpClient,
        IFaceTemplateRepository templateRepository,
        IFaceTemplateProtector templateProtector,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _templateRepository = templateRepository;
        _templateProtector = templateProtector;
        _allowPassiveDevelopmentVerification = environment.IsDevelopment() && configuration.GetValue<bool>("FaceBiometrics:AllowPassiveDevelopmentVerification");
    }

    public async Task<bool> VerifyAsync(Guid userId, Stream imageStream, CancellationToken cancellationToken = default)
    {
        var templates = await _templateRepository.GetByUserIdAsync(userId, cancellationToken);
        if (templates.Count < MinimumEnrollmentFrames)
            return false;

        var embeddings = templates.Select(template =>
        {
            var serializedEmbedding = _templateProtector.Unprotect(template);
            return JsonSerializer.Deserialize<float[]>(serializedEmbedding)
                ?? throw new DomainException("Stored face template is invalid.");
        }).ToArray();
        ValidateEmbeddings(embeddings);

        using var form = new MultipartFormDataContent();
        var streamContent = new StreamContent(imageStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        form.Add(streamContent, "image", "face.jpg");
        form.Add(new StringContent(JsonSerializer.Serialize(embeddings)), "referenceEmbeddingsJson");

        using var response = await _httpClient.PostAsync("/verify", form, cancellationToken);
        await EnsureFaceServiceSuccessAsync(response, "verification", cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<FaceVerifyResponse>(cancellationToken: cancellationToken);
        // Passive embedding comparison does not prove a live person is present.
        // It is allowed only when an explicit local-development secret is set.
        return result?.Match == true && (result.LivenessPassed || _allowPassiveDevelopmentVerification);
    }

    public async Task EnrollAsync(Guid userId, List<Stream> imageStreams, CancellationToken cancellationToken = default)
    {
        if (imageStreams.Count is < MinimumEnrollmentFrames or > MaximumEnrollmentFrames)
            throw new DomainException("Provide three to five face images for enrollment.");

        using var form = new MultipartFormDataContent();

        int i = 0;
        foreach (var stream in imageStreams)
        {
            var streamContent = new StreamContent(stream);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            form.Add(streamContent, "images", $"face_{i++}.jpg");
        }

        using var response = await _httpClient.PostAsync("/extract", form, cancellationToken);
        await EnsureFaceServiceSuccessAsync(response, "enrollment", cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<FaceExtractResponse>(cancellationToken: cancellationToken);
        var embeddings = result?.Embeddings?.ToArray() ?? [];
        ValidateEmbeddings(embeddings);
        if (embeddings.Length is < MinimumEnrollmentFrames or > MaximumEnrollmentFrames)
            throw new DomainException("Enrollment requires one clear face in each of three to five images.");

        var templates = embeddings
            .Select(embedding => _templateProtector.Protect(userId, JsonSerializer.SerializeToUtf8Bytes(embedding)))
            .ToArray();
        await _templateRepository.ReplaceForUserAsync(userId, templates, cancellationToken);
    }

    public Task DeleteEnrollmentAsync(Guid userId, CancellationToken cancellationToken = default)
        => _templateRepository.DeleteForUserAsync(userId, cancellationToken);

    private static void ValidateEmbeddings(IReadOnlyList<float[]> embeddings)
    {
        if (embeddings.Count == 0 || embeddings.Any(embedding => embedding.Length != EmbeddingDimensions || embedding.Any(value => !float.IsFinite(value))))
            throw new DomainException("Face service returned an invalid biometric template.");
    }

    private static async Task EnsureFaceServiceSuccessAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        // A 4xx response means the submitted capture failed validation (for example,
        // no face, multiple faces, an unsupported image, or an unusable frame). It is
        // not a face-service outage and must be shown honestly to the person enrolling.
        if ((int)response.StatusCode is >= 400 and < 500)
        {
            var detail = await ReadProblemDetailAsync(response, cancellationToken);
            throw new DomainException(string.IsNullOrWhiteSpace(detail)
                ? $"Face {operation} rejected this capture. Use one clear, well-lit image containing only your face."
                : detail);
        }

        response.EnsureSuccessStatusCode();
    }

    private static async Task<string?> ReadProblemDetailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return document.RootElement.TryGetProperty("detail", out var detail)
                && detail.ValueKind == JsonValueKind.String
                ? detail.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private class FaceVerifyResponse
    {
        public bool Match { get; set; }
        public float Confidence { get; set; }
        public bool LivenessPassed { get; set; }
    }

    private class FaceExtractResponse
    {
        public List<float[]>? Embeddings { get; set; }
    }
}
