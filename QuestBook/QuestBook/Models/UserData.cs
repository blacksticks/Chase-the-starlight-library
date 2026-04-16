using System;
using System.Collections.Generic;

namespace QuestBook.Models
{
    [Serializable]
    public class UserData
    {
        public List<UserChapterState> Chapters = new List<UserChapterState>();
        public List<UserCreatedChapter> CreatedChapters = new List<UserCreatedChapter>();
        public List<UserCreatedTask> CreatedTasks = new List<UserCreatedTask>();
    }

    [Serializable]
    public class UserChapterState
    {
        public string Id;
        public string IconPath;
        public string BackgroundPath;
        public List<string> CompletedNodeIds = new List<string>();
    }

    [Serializable]
    public class UserCreatedChapter
    {
        public string Id;
        public string Name;
        public string Description;
        public int Order;
        public bool VisibleByDefault = true;
    }

    [Serializable]
    public class UserCreatedTask
    {
        public string ChapterId;
        public string Id;
        public string Title;
        public string Description;
        public bool DirectUnlock;
        public bool VisibleBeforeUnlock;
        public int UnlockConditionIndex;
        public string IconPath;
        public int CompletionTypeIndex;
        public int RewardModeIndex;
        public int RewardPoolIndex;
        public float PosX;
        public float PosY;
        public List<string> PrereqNodeIds = new List<string>();
        public List<UserRewardItem> RewardItems = new List<UserRewardItem>();
    }

    [Serializable]
    public class UserRewardItem
    {
        public string ItemId;
        public int Count;
    }
}
