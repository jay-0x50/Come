using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Come.Infrastructure;
using Come.Models;
using Come.Services;

namespace Come.ViewModels;

public enum AppScreen { Attract, Home, Builder, Summary, Shipping, Payment, Complete }

public sealed class MainViewModel : ViewModelBase
{
    private readonly IPartCatalogService _catalog;
    private readonly IRemotePartCatalogService _remoteCatalog;
    private readonly ICompatibilityService _compatibilityService;
    private readonly IBuildStorageService _storage;
    private readonly IPaymentService _paymentService;
    private readonly List<PartItem> _allParts;
    private AppScreen _currentScreen = AppScreen.Attract;
    private CategoryOption _selectedCategory;
    private string _searchQuery = string.Empty;
    private string _selectedSort = "추천순";
    private PartItem? _detailPart;
    private bool _isDetailOpen;
    private CompatibilityResult _compatibility = CompatibilityResult.Empty;
    private string _toastMessage = string.Empty;
    private string _customerName = string.Empty;
    private string _phoneNumber = string.Empty;
    private string _address = string.Empty;
    private string _addressDetail = string.Empty;
    private string _shippingError = string.Empty;
    private string _paymentMethod = "신용카드";
    private bool _isPaying;
    private string _paymentStatus = "결제 수단을 선택해 주세요";
    private PaymentReceipt? _receipt;
    private bool _isCatalogSyncing;
    private string _catalogStatus = "실제 제품 데이터 연결 중";
    private int _remotePartCount;

    public MainViewModel(
        IPartCatalogService catalog,
        IRemotePartCatalogService remoteCatalog,
        ICompatibilityService compatibilityService,
        IBuildStorageService storage,
        IPaymentService paymentService)
    {
        _catalog = catalog;
        _remoteCatalog = remoteCatalog;
        _compatibilityService = compatibilityService;
        _storage = storage;
        _paymentService = paymentService;
        _allParts = _catalog.GetAll().ToList();

        Categories =
        [
            new(PartCategory.Cpu, "프로세서", "CPU", "01"),
            new(PartCategory.Mainboard, "메인보드", "MAINBOARD", "02"),
            new(PartCategory.Memory, "메모리", "MEMORY", "03"),
            new(PartCategory.Graphics, "그래픽카드", "GRAPHICS", "04"),
            new(PartCategory.Storage, "스토리지", "STORAGE", "05"),
            new(PartCategory.Power, "파워", "POWER", "06"),
            new(PartCategory.Case, "케이스", "CASE", "07"),
            new(PartCategory.Cooler, "쿨러", "COOLER", "08")
        ];
        _selectedCategory = Categories[0];

        StartCommand = new RelayCommand(Start);
        StartCustomCommand = new RelayCommand(StartCustom);
        ApplyPresetCommand = new RelayCommand(parameter => ApplyPreset(parameter?.ToString() ?? "gaming"));
        SelectCategoryCommand = new RelayCommand(parameter => SelectCategory(parameter as CategoryOption));
        ToggleTagCommand = new RelayCommand(parameter => ToggleTag(parameter?.ToString()));
        ClearSearchCommand = new RelayCommand(ClearSearch);
        OpenDetailCommand = new RelayCommand(parameter => OpenDetail(parameter as PartItem));
        CloseDetailCommand = new RelayCommand(() => IsDetailOpen = false);
        AddPartCommand = new RelayCommand(parameter => AddPart(parameter as PartItem));
        ConfirmDetailCommand = new RelayCommand(() => AddPart(DetailPart));
        RemovePartCommand = new RelayCommand(parameter => RemovePart(parameter as SelectedPartLine));
        ChangePartCommand = new RelayCommand(parameter => ChangePart(parameter as SelectedPartLine));
        GoSummaryCommand = new RelayCommand(() => CurrentScreen = AppScreen.Summary);
        GoShippingCommand = new RelayCommand(GoShipping);
        GoPaymentCommand = new RelayCommand(GoPayment);
        BackCommand = new RelayCommand(GoBack);
        HomeCommand = new RelayCommand(() => CurrentScreen = AppScreen.Home);
        SaveBuildCommand = new RelayCommand(SaveBuild);
        LoadBuildCommand = new RelayCommand(LoadBuild);
        ResetBuildCommand = new RelayCommand(ResetBuild);
        SelectPaymentMethodCommand = new RelayCommand(parameter => SelectPaymentMethod(parameter?.ToString()));
        PayCommand = new AsyncRelayCommand(PayAsync, () => !IsPaying);
        NewOrderCommand = new RelayCommand(ReturnToAttract);
        SyncCatalogCommand = new AsyncRelayCommand(SyncCatalogAsync, () => !IsCatalogSyncing);

        RefreshParts();
        _ = SyncCatalogAsync();
    }

    public IReadOnlyList<CategoryOption> Categories { get; }
    public ObservableCollection<PartItem> FilteredParts { get; } = [];
    public ObservableCollection<SelectedPartLine> SelectedParts { get; } = [];
    public ObservableCollection<string> ActiveTags { get; } = [];
    public IReadOnlyList<string> PopularTags { get; } = ["게이밍", "가성비", "고사양", "DDR5", "저소음"];
    public IReadOnlyList<string> SortOptions { get; } = ["추천순", "가격 낮은순", "가격 높은순", "성능순"];

    public ICommand StartCommand { get; }
    public ICommand StartCustomCommand { get; }
    public ICommand ApplyPresetCommand { get; }
    public ICommand SelectCategoryCommand { get; }
    public ICommand ToggleTagCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand OpenDetailCommand { get; }
    public ICommand CloseDetailCommand { get; }
    public ICommand AddPartCommand { get; }
    public ICommand ConfirmDetailCommand { get; }
    public ICommand RemovePartCommand { get; }
    public ICommand ChangePartCommand { get; }
    public ICommand GoSummaryCommand { get; }
    public ICommand GoShippingCommand { get; }
    public ICommand GoPaymentCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand HomeCommand { get; }
    public ICommand SaveBuildCommand { get; }
    public ICommand LoadBuildCommand { get; }
    public ICommand ResetBuildCommand { get; }
    public ICommand SelectPaymentMethodCommand { get; }
    public ICommand PayCommand { get; }
    public ICommand NewOrderCommand { get; }
    public ICommand SyncCatalogCommand { get; }

    public bool IsCatalogSyncing
    {
        get => _isCatalogSyncing;
        private set => SetProperty(ref _isCatalogSyncing, value);
    }

    public string CatalogStatus { get => _catalogStatus; private set => SetProperty(ref _catalogStatus, value); }
    public int RemotePartCount { get => _remotePartCount; private set => SetProperty(ref _remotePartCount, value); }

    public AppScreen CurrentScreen
    {
        get => _currentScreen;
        set
        {
            if (!SetProperty(ref _currentScreen, value)) return;
            OnPropertyChanged(nameof(IsChromeVisible));
            OnPropertyChanged(nameof(IsBuilderScreen));
            OnPropertyChanged(nameof(PageTitle));
            OnPropertyChanged(nameof(PageCaption));
            OnPropertyChanged(nameof(CurrentStep));
        }
    }

    public bool IsChromeVisible => CurrentScreen is AppScreen.Builder or AppScreen.Summary or AppScreen.Shipping or AppScreen.Payment;
    public bool IsBuilderScreen => CurrentScreen == AppScreen.Builder;

    public string PageTitle => CurrentScreen switch
    {
        AppScreen.Builder => SearchQuery.Length > 0 ? "통합 검색" : $"{SelectedCategory.Name} 선택",
        AppScreen.Summary => "나의 PC 견적",
        AppScreen.Shipping => "배송 정보",
        AppScreen.Payment => "결제하기",
        _ => string.Empty
    };

    public string PageCaption => CurrentScreen switch
    {
        AppScreen.Builder => SearchQuery.Length > 0 ? $"‘{SearchQuery}’ 검색 결과" : "원하는 부품을 비교하고 조합해 보세요",
        AppScreen.Summary => "선택한 부품과 호환성을 마지막으로 확인해 주세요",
        AppScreen.Shipping => "안전한 배송을 위해 정확한 정보를 입력해 주세요",
        AppScreen.Payment => "결제가 완료되면 바로 조립을 시작합니다",
        _ => string.Empty
    };

    public int CurrentStep => CurrentScreen switch
    {
        AppScreen.Builder => 1, AppScreen.Summary => 2, AppScreen.Shipping => 3, AppScreen.Payment => 4, _ => 0
    };

    public CategoryOption SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (!SetProperty(ref _selectedCategory, value)) return;
            OnPropertyChanged(nameof(PageTitle));
            OnPropertyChanged(nameof(SelectedCategoryCount));
            RefreshParts();
        }
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (!SetProperty(ref _searchQuery, value)) return;
            OnPropertyChanged(nameof(PageTitle));
            OnPropertyChanged(nameof(PageCaption));
            RefreshParts();
            if (CurrentScreen == AppScreen.Summary && value.Length > 0) CurrentScreen = AppScreen.Builder;
        }
    }

    public string SelectedSort
    {
        get => _selectedSort;
        set { if (SetProperty(ref _selectedSort, value)) RefreshParts(); }
    }

    public PartItem? DetailPart { get => _detailPart; private set => SetProperty(ref _detailPart, value); }
    public bool IsDetailOpen { get => _isDetailOpen; set => SetProperty(ref _isDetailOpen, value); }

    public CompatibilityResult Compatibility
    {
        get => _compatibility;
        private set
        {
            if (!SetProperty(ref _compatibility, value)) return;
            OnPropertyChanged(nameof(HasCompatibilityMessages));
            OnPropertyChanged(nameof(HasCompatibilityErrors));
            OnPropertyChanged(nameof(CompatibilityHeadline));
            OnPropertyChanged(nameof(CompatibilityDetail));
            OnPropertyChanged(nameof(CanCheckout));
        }
    }

    public bool HasCompatibilityMessages => Compatibility.Messages.Count > 0;
    public bool HasCompatibilityErrors => !Compatibility.IsCompatible;
    public string CompatibilityHeadline => HasCompatibilityErrors ? "호환되지 않는 부품이 있어요" : HasUnavailableParts ? "미리보기 전용 부품이 포함되어 있어요" : Compatibility.HasWarnings ? "확인이 필요한 항목이 있어요" : "모든 부품이 완벽하게 호환돼요";
    public string CompatibilityDetail => Compatibility.Messages.FirstOrDefault()?.Text ?? (HasUnavailableParts ? "가격 또는 재고가 없는 부품은 3D 구성만 가능하며 결제할 수 없습니다." : "선택 즉시 소켓, 규격, 크기와 전력을 자동으로 확인합니다.");

    public string ToastMessage { get => _toastMessage; private set => SetProperty(ref _toastMessage, value); }
    public bool HasToast => !string.IsNullOrWhiteSpace(ToastMessage);
    public int ResultCount => FilteredParts.Count;
    public int SelectedCategoryCount => FilteredParts.Count;
    public int SelectedCount => SelectedParts.Count;
    public string SelectedCountText => $"{SelectedCount} / 8";
    public decimal TotalPrice => SelectedParts.Sum(item => item.Part.Price);
    public decimal SupplyPrice => Math.Round(TotalPrice / 1.1m, 0);
    public decimal Vat => TotalPrice - SupplyPrice;
    public int EstimatedPower => SelectedParts.Where(item => item.Category != PartCategory.Power).Sum(item => item.Part.PowerConsumptionW);
    public int RecommendedPower => Math.Max(500, (int)Math.Ceiling(EstimatedPower * 1.2 / 50d) * 50);
    public bool HasUnavailableParts => SelectedParts.Any(item => !item.Part.CanPurchase);
    public bool CanCheckout => SelectedCount == 8 && Compatibility.IsCompatible && !HasUnavailableParts;
    public string CheckoutHint => HasUnavailableParts ? "미리보기 전용 부품이 포함되어 결제할 수 없습니다" : "8개 부품 선택 및 호환성 확인 후 진행할 수 있습니다";

    public string CustomerName { get => _customerName; set => SetProperty(ref _customerName, value); }
    public string PhoneNumber { get => _phoneNumber; set => SetProperty(ref _phoneNumber, value); }
    public string Address { get => _address; set => SetProperty(ref _address, value); }
    public string AddressDetail { get => _addressDetail; set => SetProperty(ref _addressDetail, value); }
    public string ShippingError { get => _shippingError; private set { SetProperty(ref _shippingError, value); OnPropertyChanged(nameof(HasShippingError)); } }
    public bool HasShippingError => !string.IsNullOrEmpty(ShippingError);

    public string PaymentMethod { get => _paymentMethod; private set => SetProperty(ref _paymentMethod, value); }
    public bool IsPaying { get => _isPaying; private set { SetProperty(ref _isPaying, value); OnPropertyChanged(nameof(IsNotPaying)); } }
    public bool IsNotPaying => !IsPaying;
    public string PaymentStatus { get => _paymentStatus; private set => SetProperty(ref _paymentStatus, value); }
    public PaymentReceipt? Receipt { get => _receipt; private set => SetProperty(ref _receipt, value); }

    private void Start() => CurrentScreen = AppScreen.Home;

    private void StartCustom()
    {
        CurrentScreen = AppScreen.Builder;
        SelectedCategory = Categories[0];
    }

    private void ApplyPreset(string preset)
    {
        var ids = preset switch
        {
            "creator" => new[] { "cpu-14700k", "mb-b760", "ram-5600-32", "gpu-4070s", "ssd-990-2t", "psu-850", "case-h5", "cooler-ls720" },
            "office" => new[] { "cpu-7600", "mb-a620", "ram-5600-32", "gpu-4060", "ssd-p3-1t", "psu-650", "case-h5", "cooler-pa120" },
            _ => new[] { "cpu-7800x3d", "mb-b650", "ram-6000-32", "gpu-4070s", "ssd-sn850x-2t", "psu-750", "case-north", "cooler-pa120" }
        };

        SetBuild(ids);
        CurrentScreen = AppScreen.Summary;
        ShowToast("추천 구성을 불러왔어요. 원하는 부품은 언제든 변경할 수 있습니다.");
    }

    private void SelectCategory(CategoryOption? category)
    {
        if (category is null) return;
        SearchQuery = string.Empty;
        SelectedCategory = category;
        CurrentScreen = AppScreen.Builder;
    }

    private void ToggleTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return;
        if (ActiveTags.Contains(tag)) ActiveTags.Remove(tag); else ActiveTags.Add(tag);
        RefreshParts();
    }

    private void ClearSearch()
    {
        _searchQuery = string.Empty;
        OnPropertyChanged(nameof(SearchQuery));
        ActiveTags.Clear();
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(PageCaption));
        RefreshParts();
    }

    private void OpenDetail(PartItem? part)
    {
        if (part is null) return;
        DetailPart = part;
        IsDetailOpen = true;
    }

    private void AddPart(PartItem? part)
    {
        if (part is null) return;
        var existing = SelectedParts.FirstOrDefault(item => item.Category == part.Category);
        if (existing is not null) SelectedParts.Remove(existing);
        SelectedParts.Add(new SelectedPartLine(part.Category, part.CategoryName, part.Glyph, part));
        SortSelectedParts();
        IsDetailOpen = false;
        Recalculate();
        ShowToast(part.CanPurchase
            ? $"{part.Name}을(를) 견적에 담았습니다."
            : $"{part.Name}을(를) 3D 미리보기에 추가했습니다. 결제는 지원되지 않습니다.");
    }

    private void RemovePart(SelectedPartLine? line)
    {
        if (line is null) return;
        SelectedParts.Remove(line);
        Recalculate();
    }

    private void ChangePart(SelectedPartLine? line)
    {
        if (line is null) return;
        SelectedCategory = Categories.First(category => category.Value == line.Category);
        CurrentScreen = AppScreen.Builder;
    }

    private void GoShipping()
    {
        if (!CanCheckout)
        {
            ShowToast(HasUnavailableParts ? "미리보기 전용 부품은 결제할 수 없습니다." : HasCompatibilityErrors ? "호환성 문제를 먼저 해결해 주세요." : "8개 카테고리의 부품을 모두 선택해 주세요.");
            return;
        }
        CurrentScreen = AppScreen.Shipping;
    }

    private void GoPayment()
    {
        var digits = Regex.Replace(PhoneNumber, "[^0-9]", string.Empty);
        if (string.IsNullOrWhiteSpace(CustomerName) || string.IsNullOrWhiteSpace(Address) || string.IsNullOrWhiteSpace(AddressDetail))
        {
            ShippingError = "받는 분과 배송지 정보를 모두 입력해 주세요.";
            return;
        }
        if (!Regex.IsMatch(digits, "^01[016789][0-9]{7,8}$"))
        {
            ShippingError = "휴대폰 번호 형식을 확인해 주세요.";
            return;
        }
        ShippingError = string.Empty;
        CurrentScreen = AppScreen.Payment;
    }

    private void GoBack()
    {
        CurrentScreen = CurrentScreen switch
        {
            AppScreen.Builder => AppScreen.Home,
            AppScreen.Summary => AppScreen.Builder,
            AppScreen.Shipping => AppScreen.Summary,
            AppScreen.Payment when !IsPaying => AppScreen.Shipping,
            _ => CurrentScreen
        };
    }

    private void SaveBuild()
    {
        _storage.Save(SelectedParts.Select(item => item.Part.Id));
        ShowToast("현재 견적을 이 기기에 저장했습니다.");
    }

    private void LoadBuild()
    {
        var ids = _storage.Load();
        if (ids.Count == 0) { ShowToast("저장된 견적이 없습니다."); return; }
        SetBuild(ids);
        ShowToast("최근 저장한 견적을 불러왔습니다.");
    }

    private void ResetBuild()
    {
        SelectedParts.Clear();
        Recalculate();
        CurrentScreen = AppScreen.Builder;
    }

    private void SelectPaymentMethod(string? method)
    {
        if (string.IsNullOrWhiteSpace(method) || IsPaying) return;
        PaymentMethod = method;
        PaymentStatus = $"{method} 결제를 준비했습니다";
    }

    private async Task PayAsync()
    {
        IsPaying = true;
        PaymentStatus = PaymentMethod == "신용카드" ? "카드를 리더기에 꽂아 주세요" : "화면의 QR 코드를 스캔해 주세요";
        try
        {
            Receipt = await _paymentService.PayAsync(TotalPrice, PaymentMethod);
            PaymentStatus = "결제가 승인되었습니다";
            CurrentScreen = AppScreen.Complete;
        }
        catch (Exception)
        {
            PaymentStatus = "승인에 실패했습니다. 다시 시도해 주세요";
        }
        finally { IsPaying = false; }
    }

    public void ReturnToAttract()
    {
        IsDetailOpen = false;
        SelectedParts.Clear();
        ActiveTags.Clear();
        _searchQuery = string.Empty;
        OnPropertyChanged(nameof(SearchQuery));
        CustomerName = PhoneNumber = Address = AddressDetail = string.Empty;
        Receipt = null;
        Recalculate();
        CurrentScreen = AppScreen.Attract;
    }

    private void SetBuild(IEnumerable<string> ids)
    {
        SelectedParts.Clear();
        foreach (var id in ids)
        {
            var part = _allParts.FirstOrDefault(item => item.Id == id);
            if (part is not null) SelectedParts.Add(new(part.Category, part.CategoryName, part.Glyph, part));
        }
        SortSelectedParts();
        Recalculate();
    }

    private void SortSelectedParts()
    {
        var sorted = SelectedParts.OrderBy(item => item.Category).ToArray();
        SelectedParts.Clear();
        foreach (var item in sorted) SelectedParts.Add(item);
    }

    private void Recalculate()
    {
        Compatibility = _compatibilityService.Evaluate(SelectedParts.Select(item => item.Part));
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedCountText));
        OnPropertyChanged(nameof(TotalPrice));
        OnPropertyChanged(nameof(SupplyPrice));
        OnPropertyChanged(nameof(Vat));
        OnPropertyChanged(nameof(EstimatedPower));
        OnPropertyChanged(nameof(RecommendedPower));
        OnPropertyChanged(nameof(HasUnavailableParts));
        OnPropertyChanged(nameof(CanCheckout));
        OnPropertyChanged(nameof(CheckoutHint));
        OnPropertyChanged(nameof(CompatibilityHeadline));
        OnPropertyChanged(nameof(CompatibilityDetail));
    }

    private void RefreshParts()
    {
        IEnumerable<PartItem> query = _allParts;
        if (string.IsNullOrWhiteSpace(SearchQuery)) query = query.Where(part => part.Category == SelectedCategory.Value);
        else
        {
            var words = SearchQuery.Trim().TrimStart('#').ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            query = query.Where(part => words.All(word => part.SearchText.Contains(word, StringComparison.OrdinalIgnoreCase)));
        }

        if (ActiveTags.Count > 0)
            query = query.Where(part => ActiveTags.All(tag => part.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)));

        query = SelectedSort switch
        {
            "가격 낮은순" => query.OrderBy(part => part.Price <= 0).ThenBy(part => part.Price),
            "가격 높은순" => query.OrderByDescending(part => part.Price),
            "성능순" => query.OrderByDescending(part => part.Performance),
            _ => query.OrderByDescending(part => part.Popularity)
        };

        FilteredParts.Clear();
        foreach (var part in query) FilteredParts.Add(part);
        OnPropertyChanged(nameof(ResultCount));
        OnPropertyChanged(nameof(SelectedCategoryCount));
    }

    private async Task SyncCatalogAsync()
    {
        if (IsCatalogSyncing) return;
        IsCatalogSyncing = true;
        CatalogStatus = "실제 제품 사양 동기화 중";
        try
        {
            var result = await _remoteCatalog.GetPartsAsync();
            MergeRemoteParts(result.Parts);
            RemotePartCount = result.Parts.Count;
            CatalogStatus = $"LIVE · {RemotePartCount}개 · v{result.Version}";
            RefreshParts();
        }
        catch (Exception)
        {
            CatalogStatus = "오프라인 카탈로그 사용 중";
        }
        finally
        {
            IsCatalogSyncing = false;
        }
    }

    private void MergeRemoteParts(IReadOnlyList<PartItem> remoteParts)
    {
        _allParts.RemoveAll(part => part.IsCatalogOnly);
        foreach (var remote in remoteParts)
        {
            var remoteName = NormalizeName(remote.Name);
            var localIndex = _allParts.FindIndex(part =>
            {
                var localName = NormalizeName(part.Name);
                return localName.Length >= 7 && (remoteName == localName || remoteName.Contains(localName) || localName.Contains(remoteName));
            });

            if (localIndex >= 0)
            {
                var local = _allParts[localIndex];
                _allParts[localIndex] = local with
                {
                    DataSource = remote.DataSource,
                    SourceUrl = remote.SourceUrl,
                    LastVerified = remote.LastVerified
                };
            }
            else
            {
                _allParts.Add(remote);
            }
        }
    }

    private static string NormalizeName(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private async void ShowToast(string message)
    {
        ToastMessage = message;
        OnPropertyChanged(nameof(HasToast));
        await Task.Delay(3000);
        if (ToastMessage == message)
        {
            ToastMessage = string.Empty;
            OnPropertyChanged(nameof(HasToast));
        }
    }
}
