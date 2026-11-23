namespace JurassicPix
{
    internal interface IPlayerControl
    {
        void SetInput(DinosaurInput input);
        void Attack();
        void ToggleFlight();
        void SetLeading(bool lead);
    }

    internal interface IReplicatedCreature : IPlayerControl
    {
        string Species { get; }
        bool IsControlled { get; }
        void SetControl(bool controlled);
        void BeBorn();
        void SetReplica(bool enabled);
        DinosaurPose CapturePose();
        void ApplyReplica(DinosaurPose pose, float delta);
    }
}
