using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MAria2.Core.Interfaces;
using MAria2.Core.Entities;

namespace MAria2.Core.Rules
{
    public class PostProcessingRulesEngine
    {
        private readonly List<IPostProcessingRule> _rules;
        private readonly IMediaInformationService _mediaInfoService;
        private readonly ILogger<PostProcessingRulesEngine> _logger;

        public PostProcessingRulesEngine(
            IMediaInformationService mediaInfoService,
            ILogger<PostProcessingRulesEngine> logger)
        {
            _mediaInfoService = mediaInfoService;
            _logger = logger;
            _rules = new List<IPostProcessingRule>
            {
                new VideoQualityRule(),
                new AudioNormalizationRule(),
                new SecuritySanitizationRule(),
                new MetadataCleaningRule(),
                new StorageOptimizationRule()
            };
        }

        public async Task<PostProcessingPipeline> DetermineProcessingPipelineAsync(Download download)
        {
            var metadata = await _mediaInfoService.ExtractMetadataAsync(download.FilePath);
            var applicableRules = _rules
                .Where(rule => rule.ShouldApply(metadata))
                .ToList();

            var processingSteps = new List<PostProcessingType>();
            var config = new PostProcessingConfig();

            foreach (var rule in applicableRules)
            {
                var ruleResult = await rule.EvaluateAsync(metadata);
                processingSteps.AddRange(ruleResult.RecommendedProcessingTypes);
                
                // Merge configurations
                MergeConfigurations(config, ruleResult.SuggestedConfiguration);
            }

            return new PostProcessingPipeline
            {
                PipelineId = Guid.NewGuid(),
                Tasks = processingSteps.Select(step => new PostProcessingTask 
                { 
                    Type = step, 
                    Status = ProcessingTaskStatus.Pending 
                }).ToList(),
                CreatedAt = DateTime.UtcNow,
                Status = ProcessingPipelineStatus.Created
            };
        }

        private void MergeConfigurations(PostProcessingConfig baseConfig, PostProcessingConfig newConfig)
        {
            // Merge configuration logic
            baseConfig.VideoTranscode ??= newConfig.VideoTranscode;
            baseConfig.AudioNormalization ??= newConfig.AudioNormalization;
            // Add more merging for other configuration types
        }
    }

    public interface IPostProcessingRule
    {
        bool ShouldApply(MediaMetadata metadata);
        Task<PostProcessingRuleResult> EvaluateAsync(MediaMetadata metadata);
    }

    public class PostProcessingRuleResult
    {
        public List<PostProcessingType> RecommendedProcessingTypes { get; set; }
        public PostProcessingConfig SuggestedConfiguration { get; set; }
        public string Rationale { get; set; }
    }

    // Specific Rule Implementations
    public class VideoQualityRule : IPostProcessingRule
    {
        public bool ShouldApply(MediaMetadata metadata) =>
            metadata.Type == MediaType.Video && 
            (metadata.Quality == "Low" || metadata.BitRate < 2000);

        public async Task<PostProcessingRuleResult> EvaluateAsync(MediaMetadata metadata)
        {
            return new PostProcessingRuleResult
            {
                RecommendedProcessingTypes = new List<PostProcessingType>
                {
                    PostProcessingType.VideoTranscode,
                    PostProcessingType.AIEnhancement
                },
                SuggestedConfiguration = new PostProcessingConfig
                {
                    VideoTranscode = new VideoTranscodeOptions
                    {
                        TargetBitrate = 5000,
                        Resolution = "1920x1080"
                    },
                    AIEnhancement = new AIEnhancementOptions
                    {
                        EnhanceVideoQuality = true,
                        UpscaleResolution = true,
                        NoiseReduction = true
                    }
                },
                Rationale = "Low video quality detected. Recommending transcoding and AI enhancement."
            };
        }
    }

    public class AudioNormalizationRule : IPostProcessingRule
    {
        public bool ShouldApply(MediaMetadata metadata) =>
            metadata.Type == MediaType.Audio && 
            (metadata.BitRate < 192 || metadata.SampleRate < 44100);

        public async Task<PostProcessingRuleResult> EvaluateAsync(MediaMetadata metadata)
        {
            return new PostProcessingRuleResult
            {
                RecommendedProcessingTypes = new List<PostProcessingType>
                {
                    PostProcessingType.AudioNormalization
                },
                SuggestedConfiguration = new PostProcessingConfig
                {
                    AudioNormalization = new AudioNormalizationOptions
                    {
                        TargetVolume = -14.0,
                        ApplyCompression = true,
                        ReduceDynamicRange = true
                    }
                },
                Rationale = "Low audio quality detected. Recommending audio normalization."
            };
        }
    }

    public class SecuritySanitizationRule : IPostProcessingRule
    {
        public bool ShouldApply(MediaMetadata metadata) => true;

        public async Task<PostProcessingRuleResult> EvaluateAsync(MediaMetadata metadata)
        {
            return new PostProcessingRuleResult
            {
                RecommendedProcessingTypes = new List<PostProcessingType>
                {
                    PostProcessingType.SecuritySanitization,
                    PostProcessingType.MetadataCleaning
                },
                SuggestedConfiguration = new PostProcessingConfig
                {
                    SecuritySanitization = new SecuritySanitizationOptions
                    {
                        RemoveExecutables = true,
                        ScanForMalware = true,
                        AnonymizeMetadata = true
                    },
                    MetadataCleaning = new MetadataCleaningOptions
                    {
                        RemovePersonalInfo = true,
                        StandardizeMetadata = true
                    }
                },
                Rationale = "Applying standard security and metadata sanitization."
            };
        }
    }

    public class MetadataCleaningRule : IPostProcessingRule
    {
        public bool ShouldApply(MediaMetadata metadata) => 
            !string.IsNullOrEmpty(metadata.CopyrightInfo);

        public async Task<PostProcessingRuleResult> EvaluateAsync(MediaMetadata metadata)
        {
            return new PostProcessingRuleResult
            {
                RecommendedProcessingTypes = new List<PostProcessingType>
                {
                    PostProcessingType.MetadataCleaning
                },
                SuggestedConfiguration = new PostProcessingConfig
                {
                    MetadataCleaning = new MetadataCleaningOptions
                    {
                        RemovePersonalInfo = true,
                        StandardizeMetadata = true,
                        MetadataFieldsToRemove = new[] 
                        { 
                            "PersonalIdentifiers", 
                            "PrivateComments" 
                        }
                    }
                },
                Rationale = "Copyright metadata detected. Applying metadata cleaning."
            };
        }
    }

    public class StorageOptimizationRule : IPostProcessingRule
    {
        public bool ShouldApply(MediaMetadata metadata) => 
            metadata.FileSize > 1_000_000_000; // 1GB

        public async Task<PostProcessingRuleResult> EvaluateAsync(MediaMetadata metadata)
        {
            return new PostProcessingRuleResult
            {
                RecommendedProcessingTypes = new List<PostProcessingType>
                {
                    PostProcessingType.FileCompression
                },
                SuggestedConfiguration = new PostProcessingConfig
                {
                    FileCompression = new FileCompressionOptions
                    {
                        CompressionFormat = "zip",
                        CompressionLevel = 7,
                        SplitArchive = true,
                        MaxArchiveSize = 500_000_000 // 500MB
                    }
                },
                Rationale = "Large file detected. Recommending file compression."
            };
        }
    }
}
