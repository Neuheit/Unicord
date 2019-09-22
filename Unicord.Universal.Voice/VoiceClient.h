﻿#pragma once

#include "VoiceClient.g.h"
#include "SodiumWrapper.h"
#include "ConnectionEndpoint.h"
#include "AudioFormat.h"
#include "AudioRenderer.h"
#include "OpusWrapper.h"

#include <opus.h>
#include <sodium.h>
#include <string>
#include <iostream>
#include <chrono>
#include <sstream>
#include <thread>
#include <debugapi.h>
#include <concurrent_unordered_map.h>
#include <concurrent_queue.h>
#include <winrt/Windows.Data.Json.h>
#include <winrt/Windows.Storage.Streams.h>

using namespace winrt;
using namespace winrt::Windows::Foundation;
using namespace winrt::Windows::Data::Json;
using namespace winrt::Windows::System::Threading;
using namespace winrt::Windows::Storage::Streams;
using namespace winrt::Windows::Networking::Sockets;
using namespace winrt::Unicord::Universal::Voice::Interop;
using namespace winrt::Unicord::Universal::Voice::Render;

namespace winrt::Unicord::Universal::Voice::implementation
{
    struct VoiceClient : VoiceClientT<VoiceClient>
    {
    public:
        VoiceClient() = default;
        VoiceClient(VoiceClientOptions const& options);

        AudioFormat audio_format;

        static hstring OpusVersion();
        static hstring SodiumVersion();

        uint32_t WebSocketPing();
        uint32_t UdpSocketPing();

        event_token WebSocketPingUpdated(EventHandler<uint32_t> const& handler);
        void WebSocketPingUpdated(winrt::event_token const& token) noexcept;
        event_token UdpSocketPingUpdated(EventHandler<uint32_t> const& handler);
        void UdpSocketPingUpdated(winrt::event_token const& token) noexcept;
        event_token Connected(EventHandler<bool> const& handler);
        void Connected(event_token const& token) noexcept;
        event_token Disconnected(EventHandler<bool> const& handler);
        void Disconnected(event_token const& token) noexcept;

        IAsyncAction ConnectAsync();
        IAsyncAction SendSpeakingAsync(bool speaking);
        IOutputStream GetOutputStream();
        void UpdateAudioDevices();
        void Close();

        bool Muted();
        void Muted(bool value);
        bool Deafened();
        void Deafened(bool value);

        VoicePacket PreparePacket(array_view<uint8_t> pcm, bool silence = false, bool is_float = false);
        void EnqueuePacket(PCMPacket packet);

        ~VoiceClient();
    private:
        VoiceClientOptions options{ nullptr };
        MessageWebSocket web_socket{ nullptr };
        DatagramSocket udp_socket{ nullptr };
        ThreadPoolTimer heartbeat_timer{ nullptr };
        ThreadPoolTimer keepalive_timer{ nullptr };
        SodiumWrapper* sodium = nullptr;
        OpusWrapper* opus = nullptr;
        AudioRenderer* renderer = nullptr;

        std::pair<hstring, EncryptionMode> mode;
        ConnectionEndpoint ws_endpoint;
        ConnectionEndpoint udp_endpoint;

        bool is_speaking = false;
        bool is_muted = false;
        bool is_deafened = false;

        bool ws_closed = true;
        bool can_resume = false;
        winrt::event<EventHandler<bool>> connected;
        winrt::event<EventHandler<bool>> disconnected;

        uint16_t seq = 0;
        uint32_t ssrc = 0;
        uint32_t timestamp = 0;
        uint32_t nonce = 0;

        uint32_t heartbeat_interval = 0;
        uint32_t connection_stage = 0;

        volatile uint32_t ws_ping = 0;
        volatile uint32_t last_heartbeat = 0;
        winrt::event<EventHandler<uint32_t>> wsPingUpdated;

        volatile uint32_t udp_ping = 0;
        volatile uint64_t keepalive_count = 0;
        winrt::event<EventHandler<uint32_t>> udpPingUpdated;

        concurrency::concurrent_unordered_map<uint64_t, uint64_t> keepalive_timestamps;
        concurrency::concurrent_queue<PCMPacket> voice_queue;
        volatile bool cancel_voice_send = false;
        std::thread voice_thread;

        bool is_disposed = false;

        void InitialiseSockets();
        IAsyncAction SendIdentifyAsync(bool isResume = false);
        IAsyncAction SendJsonPayloadAsync(JsonObject &payload);
        IAsyncAction Stage1(JsonObject obj);
        void Stage2(JsonObject obj);
        IAsyncAction Stage3(std::string &ip, const uint16_t &port);

        void VoiceSendLoop();

        void ProcessRawPacket(array_view<uint8_t> data);
        bool ProcessIncomingPacket(array_view<const uint8_t> data, std::vector<std::vector<uint8_t>> &pcm, AudioSource** source);

        bool DecodeOpusPacket(const RtpHeader &header, AudioSource** source, array_view<const uint8_t> &encrypted_data, const array_view<uint8_t> &nonce_view, std::vector<std::vector<uint8_t>> & pcm);

        IAsyncAction OnWsHeartbeat(ThreadPoolTimer sender);
        IAsyncAction OnWsMessage(IWebSocket socket, MessageWebSocketMessageReceivedEventArgs ev);
        IAsyncAction OnWsClosed(IWebSocket socket, WebSocketClosedEventArgs ev);
        IAsyncAction ReconnectLoop();

        IAsyncAction OnUdpHeartbeat(ThreadPoolTimer sender);
        IAsyncAction OnUdpMessage(DatagramSocket socket, DatagramSocketMessageReceivedEventArgs ev);
        void HandleUdpHeartbeat(uint64_t reader);
        void Reset();
    };
}

class dbg_stream_for_cout : public std::stringbuf
{
public:
    virtual int_type overflow(int_type c = EOF) {
        if (c != EOF) {
            TCHAR buf[] = { c, '\0' };
            OutputDebugString(buf);
        }
        return c;
    }
};

namespace winrt::Unicord::Universal::Voice::factory_implementation
{
    struct VoiceClient : VoiceClientT<VoiceClient, implementation::VoiceClient>
    {
    };
}