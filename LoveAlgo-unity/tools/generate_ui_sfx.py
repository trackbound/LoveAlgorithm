"""
LoveAlgo UI SFX 생성기
- 타이핑: 부드러운 틱 소리 (피치 변화용 베이스)
- 호버: 가벼운 핑 소리
- 클릭: 확실한 팝 소리
"""

import numpy as np
from scipy.io import wavfile
import os

# 출력 경로
OUTPUT_DIR = r"c:\Users\podola\repos\LoveAlgo-unity\Assets\Resources\Audio\SFX"
SAMPLE_RATE = 44100

def ensure_dir():
    os.makedirs(OUTPUT_DIR, exist_ok=True)

def normalize(audio):
    """오디오 정규화 (-1 ~ 1)"""
    max_val = np.max(np.abs(audio))
    if max_val > 0:
        audio = audio / max_val
    return audio

def apply_envelope(audio, attack=0.01, decay=0.1, sustain_level=0.3, release=0.1):
    """ADSR 엔벨로프 적용"""
    length = len(audio)
    envelope = np.ones(length)
    
    attack_samples = int(attack * SAMPLE_RATE)
    decay_samples = int(decay * SAMPLE_RATE)
    release_samples = int(release * SAMPLE_RATE)
    
    # Attack
    if attack_samples > 0:
        envelope[:attack_samples] = np.linspace(0, 1, attack_samples)
    
    # Decay
    decay_end = attack_samples + decay_samples
    if decay_samples > 0 and decay_end < length:
        envelope[attack_samples:decay_end] = np.linspace(1, sustain_level, decay_samples)
    
    # Sustain (이미 sustain_level)
    if decay_end < length - release_samples:
        envelope[decay_end:length - release_samples] = sustain_level
    
    # Release
    if release_samples > 0:
        envelope[-release_samples:] = np.linspace(sustain_level, 0, release_samples)
    
    return audio * envelope

def fade_out(audio, duration=0.05):
    """페이드아웃"""
    fade_samples = int(duration * SAMPLE_RATE)
    if fade_samples > len(audio):
        fade_samples = len(audio)
    fade = np.linspace(1, 0, fade_samples)
    audio[-fade_samples:] *= fade
    return audio

def generate_typing_sound():
    """
    타이핑 사운드 - 부드러운 틱/펑 소리
    게임에서 피치를 랜덤하게 변경해서 재생할 베이스 사운드
    """
    duration = 0.08  # 80ms - 짧고 경쾌하게
    t = np.linspace(0, duration, int(SAMPLE_RATE * duration), False)
    
    # 메인 톤 (높은 주파수 - 귀여운 느낌)
    freq = 880  # A5
    main = np.sin(2 * np.pi * freq * t) * 0.6
    
    # 하모닉 추가 (부드러운 느낌)
    harmonic = np.sin(2 * np.pi * freq * 2 * t) * 0.2
    harmonic2 = np.sin(2 * np.pi * freq * 3 * t) * 0.1
    
    audio = main + harmonic + harmonic2
    
    # 빠른 어택, 빠른 릴리즈
    audio = apply_envelope(audio, attack=0.005, decay=0.02, sustain_level=0.3, release=0.03)
    audio = fade_out(audio, 0.02)
    audio = normalize(audio) * 0.7
    
    return audio

def generate_hover_sound():
    """
    호버 사운드 - 가벼운 핑/띵 소리
    마우스 오버 시 피드백
    """
    duration = 0.15  # 150ms
    t = np.linspace(0, duration, int(SAMPLE_RATE * duration), False)
    
    # 부드러운 벨 소리 느낌
    freq = 1200  # 높고 맑은 톤
    main = np.sin(2 * np.pi * freq * t) * 0.5
    
    # 약간의 배음 (벨 느낌)
    harmonic = np.sin(2 * np.pi * freq * 2.5 * t) * 0.15
    harmonic2 = np.sin(2 * np.pi * freq * 4 * t) * 0.05
    
    audio = main + harmonic + harmonic2
    
    # 부드러운 어택, 자연스러운 감쇠
    audio = apply_envelope(audio, attack=0.01, decay=0.08, sustain_level=0.2, release=0.05)
    audio = fade_out(audio, 0.03)
    audio = normalize(audio) * 0.5
    
    return audio

def generate_click_sound():
    """
    클릭 사운드 - 확실한 팝/딸깍 소리
    버튼 클릭 피드백
    """
    duration = 0.1  # 100ms
    t = np.linspace(0, duration, int(SAMPLE_RATE * duration), False)
    
    # 낮은 톤 + 높은 톤 조합 (팝 느낌)
    freq_low = 400
    freq_high = 800
    
    low = np.sin(2 * np.pi * freq_low * t) * 0.4
    high = np.sin(2 * np.pi * freq_high * t) * 0.5
    
    # 클릭감을 위한 노이즈
    noise = np.random.uniform(-0.1, 0.1, len(t))
    noise = apply_envelope(noise, attack=0.001, decay=0.01, sustain_level=0, release=0.01)
    
    audio = low + high + noise
    
    # 매우 빠른 어택, 빠른 감쇠
    audio = apply_envelope(audio, attack=0.002, decay=0.03, sustain_level=0.2, release=0.04)
    audio = fade_out(audio, 0.02)
    audio = normalize(audio) * 0.7
    
    return audio

def generate_typing_variants():
    """
    타이핑 피치 변형 버전들 (선택사항)
    Unity에서 피치 변경하는 것보다 미리 만들어두면 더 자연스러울 수 있음
    """
    variants = []
    base_freq = 880
    
    # 5가지 피치 변형
    pitch_multipliers = [0.9, 0.95, 1.0, 1.05, 1.1]
    
    for i, mult in enumerate(pitch_multipliers):
        duration = 0.08
        t = np.linspace(0, duration, int(SAMPLE_RATE * duration), False)
        
        freq = base_freq * mult
        main = np.sin(2 * np.pi * freq * t) * 0.6
        harmonic = np.sin(2 * np.pi * freq * 2 * t) * 0.2
        
        audio = main + harmonic
        audio = apply_envelope(audio, attack=0.005, decay=0.02, sustain_level=0.3, release=0.03)
        audio = fade_out(audio, 0.02)
        audio = normalize(audio) * 0.7
        
        variants.append((f"Type_{i+1}", audio))
    
    return variants

def save_wav(filename, audio):
    """WAV 파일로 저장"""
    filepath = os.path.join(OUTPUT_DIR, filename)
    # 16비트 정수로 변환
    audio_int = (audio * 32767).astype(np.int16)
    wavfile.write(filepath, SAMPLE_RATE, audio_int)
    print(f"✓ 생성: {filepath}")

def main():
    print("=" * 50)
    print("LoveAlgo UI SFX 생성기")
    print("=" * 50)
    
    ensure_dir()
    
    # 기본 사운드 생성
    print("\n[기본 UI 사운드]")
    save_wav("Type.wav", generate_typing_sound())
    save_wav("Hover.wav", generate_hover_sound())
    save_wav("Click.wav", generate_click_sound())
    
    # 타이핑 변형 (선택사항 - Unity 피치 변경 대신 사용 가능)
    print("\n[타이핑 피치 변형]")
    for name, audio in generate_typing_variants():
        save_wav(f"{name}.wav", audio)
    
    print("\n" + "=" * 50)
    print("완료! Unity에서 사용하세요:")
    print("  - Type.wav: 타이핑 (Unity에서 피치 0.9~1.1 랜덤)")
    print("  - Hover.wav: 마우스 호버")
    print("  - Click.wav: 버튼 클릭")
    print("  - Type_1~5.wav: 피치 변형 버전 (선택사항)")
    print("=" * 50)

if __name__ == "__main__":
    main()
