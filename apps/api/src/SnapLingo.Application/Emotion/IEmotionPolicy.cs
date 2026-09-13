using SnapLingo.Domain.Enums;
using SnapLingo.Domain.ValueObjects;

namespace SnapLingo.Application.Emotion;

/// <summary>Looks up the learning-mood configuration for a user-selected emotion (spec section 3).</summary>
public interface IEmotionPolicy
{
    EmotionProfile GetProfile(EmotionType emotion, string cefrLevel);
}
