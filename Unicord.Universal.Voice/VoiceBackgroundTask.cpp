#include "pch.h"
#include "VoiceBackgroundTask.h"
#include "VoiceBackgroundTask.g.cpp"

using namespace winrt::Windows::ApplicationModel::Background;

namespace winrt::Unicord::Universal::Voice::Background::implementation
{
	void VoiceBackgroundTask::Run(IBackgroundTaskInstance const& taskInstance)
    {
		this->taskDeferral = taskInstance.GetDeferral();
		taskInstance.Canceled({ this, &VoiceBackgroundTask::OnCancelled });
    }
	void VoiceBackgroundTask::OnCancelled(Windows::ApplicationModel::Background::IBackgroundTaskInstance sender, Windows::ApplicationModel::Background::BackgroundTaskCancellationReason reason)
	{
		taskDeferral.Complete();
	}
}
