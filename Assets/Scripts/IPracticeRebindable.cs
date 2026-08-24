/// <summary>
/// Implemented by every Stage1 minigame's Visuals glue script (Stage1WaveRideVisuals,
/// Stage1QuickTapVisuals, Stage1SurviveVisuals, Stage1AntPourVisuals) so Stage1PracticeGate can
/// hand a stage's visuals off between practice-segment Directors and the main Director without
/// needing to know which concrete minigame it's driving.
/// </summary>
public interface IPracticeRebindable
{
    void BindDirector(CallDirector newDirector);
}
