using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using TimeTracker.Application.Repositories;
using TimeTracker.Desktop.Infrastructure;
using TimeTracker.Domain.Dtos;

namespace TimeTracker.Desktop.ProjectManagement;

public sealed class ProjectManagementViewModel : INotifyPropertyChanged
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IUiDispatcher _dispatcher;
    private readonly ILogger<ProjectManagementViewModel> _logger;

    private readonly RelayCommand _addCustomerCommand;
    private readonly RelayCommand _saveCustomerCommand;
    private readonly RelayCommand _addProjectCommand;
    private readonly RelayCommand _saveProjectCommand;
    private readonly RelayCommand _refreshCommand;

    private bool _isInitialized;
    private bool _isBusy;
    private bool _hasChanges;
    private string _statusMessage = "Manage customers and projects.";
    private CustomerItem _selectedCustomer;
    private ProjectItem _selectedProject;
    private string _newCustomerName = string.Empty;
    private string _customerNameInput = string.Empty;
    private bool _selectedCustomerIsArchived;
    private string _newProjectName = string.Empty;
    private string _projectNameInput = string.Empty;
    private CustomerItem _selectedProjectCustomer;
    private bool _selectedProjectIsActive = true;
    private Guid? _pendingProjectSelection;

    public ProjectManagementViewModel(
        ICustomerRepository customerRepository,
        IProjectRepository projectRepository,
        IUiDispatcher dispatcher,
        ILogger<ProjectManagementViewModel> logger)
    {
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _projectRepository = projectRepository ?? throw new ArgumentNullException(nameof(projectRepository));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        Customers = new ObservableCollection<CustomerItem>();
        Projects = new ObservableCollection<ProjectItem>();

        _addCustomerCommand = new RelayCommand(_ => _ = AddCustomerAsync(), _ => CanAddCustomer());
        _saveCustomerCommand = new RelayCommand(_ => _ = SaveCustomerAsync(), _ => CanSaveCustomer());
        _addProjectCommand = new RelayCommand(_ => _ = AddProjectAsync(), _ => CanAddProject());
        _saveProjectCommand = new RelayCommand(_ => _ = SaveProjectAsync(), _ => CanSaveProject());
        _refreshCommand = new RelayCommand(_ => _ = ReloadAsync(), _ => !IsBusy);

        AddCustomerCommand = _addCustomerCommand;
        SaveCustomerCommand = _saveCustomerCommand;
        AddProjectCommand = _addProjectCommand;
        SaveProjectCommand = _saveProjectCommand;
        RefreshCommand = _refreshCommand;
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public ObservableCollection<CustomerItem> Customers { get; }

    public ObservableCollection<ProjectItem> Projects { get; }

    public bool HasChanges
    {
        get => _hasChanges;
        private set => SetProperty(ref _hasChanges, value);
    }

    public bool HasSelectedCustomer => SelectedCustomer is not null;

    public bool HasSelectedProject => SelectedProject is not null;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                UpdateCommandStates();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string NewCustomerName
    {
        get => _newCustomerName;
        set
        {
            if (SetProperty(ref _newCustomerName, value))
            {
                UpdateCommandStates();
            }
        }
    }

    public string CustomerNameInput
    {
        get => _customerNameInput;
        set => SetProperty(ref _customerNameInput, value);
    }

    public bool SelectedCustomerIsArchived
    {
        get => _selectedCustomerIsArchived;
        set => SetProperty(ref _selectedCustomerIsArchived, value);
    }

    public string NewProjectName
    {
        get => _newProjectName;
        set
        {
            if (SetProperty(ref _newProjectName, value))
            {
                UpdateCommandStates();
            }
        }
    }

    public string ProjectNameInput
    {
        get => _projectNameInput;
        set => SetProperty(ref _projectNameInput, value);
    }

    public CustomerItem SelectedProjectCustomer
    {
        get => _selectedProjectCustomer;
        set
        {
            if (SetProperty(ref _selectedProjectCustomer, value))
            {
                UpdateCommandStates();
            }
        }
    }

    public bool SelectedProjectIsActive
    {
        get => _selectedProjectIsActive;
        set => SetProperty(ref _selectedProjectIsActive, value);
    }

    public CustomerItem SelectedCustomer
    {
        get => _selectedCustomer;
        set
        {
            if (SetProperty(ref _selectedCustomer, value))
            {
                CustomerNameInput = value?.Name ?? string.Empty;
                SelectedCustomerIsArchived = value?.IsArchived ?? false;
                NewProjectName = string.Empty;

                UpdateCommandStates();
                OnPropertyChanged(nameof(HasSelectedCustomer));

                var preferredProject = _pendingProjectSelection;
                _pendingProjectSelection = null;
                _ = LoadProjectsForCustomerAsync(value?.Id, preferredProject);
            }
        }
    }

    public ProjectItem SelectedProject
    {
        get => _selectedProject;
        set
        {
            if (SetProperty(ref _selectedProject, value))
            {
                ProjectNameInput = value?.Name ?? string.Empty;
                SelectedProjectIsActive = value?.IsActive ?? true;
                SelectedProjectCustomer = value is null
                    ? SelectedCustomer
                    : Customers.FirstOrDefault(c => c.Id == value.CustomerId) ?? SelectedCustomer;

                UpdateCommandStates();
                OnPropertyChanged(nameof(HasSelectedProject));
            }
        }
    }

    public ICommand AddCustomerCommand { get; }

    public ICommand SaveCustomerCommand { get; }

    public ICommand AddProjectCommand { get; }

    public ICommand SaveProjectCommand { get; }

    public ICommand RefreshCommand { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
        {
            await ReloadAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        _isInitialized = true;
        await RunWithBusyGuard(() => LoadCustomersAsync(null, null, cancellationToken)).ConfigureAwait(false);
    }

    public Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        var targetCustomer = SelectedCustomer?.Id;
        var targetProject = SelectedProject?.Id;
        return RunWithBusyGuard(() => LoadCustomersAsync(targetCustomer, targetProject, cancellationToken));
    }

    private bool CanAddCustomer() => !IsBusy && !string.IsNullOrWhiteSpace(NewCustomerName);

    private bool CanSaveCustomer() => !IsBusy && SelectedCustomer is not null;

    private bool CanAddProject() => !IsBusy && SelectedCustomer is not null && !string.IsNullOrWhiteSpace(NewProjectName);

    private bool CanSaveProject() => !IsBusy && SelectedProject is not null && (SelectedProjectCustomer is not null || SelectedCustomer is not null);

    private Task AddCustomerAsync()
    {
        return RunWithBusyGuard(async () =>
        {
            var name = (NewCustomerName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                await RunOnUiThreadAsync(() => StatusMessage = "Customer name is required.").ConfigureAwait(false);
                return;
            }

            if (Customers.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                await RunOnUiThreadAsync(() => StatusMessage = "A customer with that name already exists.").ConfigureAwait(false);
                return;
            }

            try
            {
                var created = await _customerRepository.CreateAsync(new CustomerCreateDto(name)).ConfigureAwait(false);

                await RunOnUiThreadAsync(() =>
                {
                    HasChanges = true;
                    NewCustomerName = string.Empty;
                    StatusMessage = $"Customer \"{created.Name}\" created.";
                }).ConfigureAwait(false);

                await LoadCustomersAsync(created.Id, null, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create customer.");
                await RunOnUiThreadAsync(() => StatusMessage = "Failed to create customer.").ConfigureAwait(false);
            }
        });
    }

    private Task SaveCustomerAsync()
    {
        return RunWithBusyGuard(async () =>
        {
            if (SelectedCustomer is null)
            {
                await RunOnUiThreadAsync(() => StatusMessage = "Select a customer to update.").ConfigureAwait(false);
                return;
            }

            var name = (CustomerNameInput ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                await RunOnUiThreadAsync(() => StatusMessage = "Customer name is required.").ConfigureAwait(false);
                return;
            }

            if (Customers.Any(c => c.Id != SelectedCustomer.Id && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                await RunOnUiThreadAsync(() => StatusMessage = "Another customer already uses that name.").ConfigureAwait(false);
                return;
            }

            try
            {
                var updated = await _customerRepository.UpdateAsync(new CustomerUpdateDto(
                    SelectedCustomer.Id,
                    name,
                    SelectedCustomerIsArchived)).ConfigureAwait(false);

                if (updated is null)
                {
                    await RunOnUiThreadAsync(() => StatusMessage = "Customer no longer exists.").ConfigureAwait(false);
                    return;
                }

                await RunOnUiThreadAsync(() =>
                {
                    HasChanges = true;
                    StatusMessage = "Customer updated.";
                }).ConfigureAwait(false);

                await LoadCustomersAsync(updated.Id, SelectedProject?.Id, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update customer.");
                await RunOnUiThreadAsync(() => StatusMessage = "Failed to update customer.").ConfigureAwait(false);
            }
        });
    }

    private Task AddProjectAsync()
    {
        return RunWithBusyGuard(async () =>
        {
            if (SelectedCustomer is null)
            {
                await RunOnUiThreadAsync(() => StatusMessage = "Select a customer before adding a project.").ConfigureAwait(false);
                return;
            }

            var name = (NewProjectName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                await RunOnUiThreadAsync(() => StatusMessage = "Project name is required.").ConfigureAwait(false);
                return;
            }

            if (Projects.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                await RunOnUiThreadAsync(() => StatusMessage = "A project with that name already exists for this customer.").ConfigureAwait(false);
                return;
            }

            try
            {
                var created = await _projectRepository.CreateAsync(new ProjectCreateDto(
                    SelectedCustomer.Id,
                    name,
                    IsActive: true)).ConfigureAwait(false);

                await RunOnUiThreadAsync(() =>
                {
                    HasChanges = true;
                    NewProjectName = string.Empty;
                    StatusMessage = $"Project \"{created.Name}\" created.";
                }).ConfigureAwait(false);

                await LoadCustomersAsync(SelectedCustomer.Id, created.Id, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create project.");
                await RunOnUiThreadAsync(() => StatusMessage = "Failed to create project.").ConfigureAwait(false);
            }
        });
    }

    private Task SaveProjectAsync()
    {
        return RunWithBusyGuard(async () =>
        {
            if (SelectedProject is null)
            {
                await RunOnUiThreadAsync(() => StatusMessage = "Select a project to update.").ConfigureAwait(false);
                return;
            }

            var targetCustomer = SelectedProjectCustomer ?? SelectedCustomer;
            if (targetCustomer is null)
            {
                await RunOnUiThreadAsync(() => StatusMessage = "Choose a customer for the project.").ConfigureAwait(false);
                return;
            }

            var name = (ProjectNameInput ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                await RunOnUiThreadAsync(() => StatusMessage = "Project name is required.").ConfigureAwait(false);
                return;
            }

            var duplicate = Projects.Any(p =>
                p.Id != SelectedProject.Id &&
                p.CustomerId == targetCustomer.Id &&
                string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

            if (duplicate)
            {
                await RunOnUiThreadAsync(() => StatusMessage = "Another project with that name already exists for the selected customer.").ConfigureAwait(false);
                return;
            }

            try
            {
                var updated = await _projectRepository.UpdateAsync(new ProjectUpdateDto(
                    SelectedProject.Id,
                    targetCustomer.Id,
                    name,
                    SelectedProjectIsActive)).ConfigureAwait(false);

                if (updated is null)
                {
                    await RunOnUiThreadAsync(() => StatusMessage = "Project no longer exists.").ConfigureAwait(false);
                    return;
                }

                await RunOnUiThreadAsync(() =>
                {
                    HasChanges = true;
                    StatusMessage = "Project updated.";
                }).ConfigureAwait(false);

                await LoadCustomersAsync(targetCustomer.Id, updated.Id, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update project.");
                await RunOnUiThreadAsync(() => StatusMessage = "Failed to update project.").ConfigureAwait(false);
            }
        });
    }

    private async Task LoadCustomersAsync(Guid? preferredCustomerId, Guid? preferredProjectId, CancellationToken cancellationToken)
    {
        try
        {
            var customers = await _customerRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
            var ordered = customers
                .OrderBy(c => c.IsArchived)
                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Select(c => new CustomerItem(c.Id, c.Name, c.IsArchived))
                .ToList();

            await RunOnUiThreadAsync(() =>
            {
                Customers.Clear();
                foreach (var item in ordered)
                {
                    Customers.Add(item);
                }

                if (Customers.Count == 0)
                {
                    SelectedCustomer = null;
                    Projects.Clear();
                    SelectedProject = null;
                    StatusMessage = "Add a customer to begin.";
                    return;
                }

                var selection = preferredCustomerId.HasValue
                    ? Customers.FirstOrDefault(c => c.Id == preferredCustomerId.Value)
                    : null;

                selection ??= Customers.FirstOrDefault(c => !c.IsArchived) ?? Customers.First();

                _pendingProjectSelection = preferredProjectId;
                SelectedCustomer = selection;
                StatusMessage = "Select or edit a project.";
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load customers.");
            await RunOnUiThreadAsync(() => StatusMessage = "Failed to load customers.").ConfigureAwait(false);
        }
    }

    private async Task LoadProjectsForCustomerAsync(Guid? customerId, Guid? preferredProjectId, CancellationToken cancellationToken = default)
    {
        if (customerId is null)
        {
            await RunOnUiThreadAsync(() =>
            {
                Projects.Clear();
                SelectedProject = null;
                ProjectNameInput = string.Empty;
                SelectedProjectIsActive = true;
                SelectedProjectCustomer = null;
            }).ConfigureAwait(false);
            return;
        }

        try
        {
            var projects = await _projectRepository
                .GetByCustomerAsync(customerId.Value, includeInactive: true, cancellationToken)
                .ConfigureAwait(false);

            var ordered = projects
                .OrderByDescending(p => p.IsActive)
                .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Select(p => new ProjectItem(
                    p.Id,
                    p.CustomerId,
                    p.Name,
                    p.IsActive,
                    Customers.FirstOrDefault(c => c.Id == p.CustomerId)?.Name ?? string.Empty))
                .ToList();

            await RunOnUiThreadAsync(() =>
            {
                Projects.Clear();
                foreach (var item in ordered)
                {
                    Projects.Add(item);
                }

                ProjectItem selection = null;
                if (preferredProjectId.HasValue)
                {
                    selection = Projects.FirstOrDefault(p => p.Id == preferredProjectId.Value);
                }

                selection ??= Projects.FirstOrDefault();
                SelectedProject = selection;

                if (selection is null)
                {
                    ProjectNameInput = string.Empty;
                    SelectedProjectIsActive = true;
                    SelectedProjectCustomer = SelectedCustomer;
                }
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load projects for customer {CustomerId}.", customerId);
            await RunOnUiThreadAsync(() => StatusMessage = "Failed to load projects.").ConfigureAwait(false);
        }
    }

    private Task RunWithBusyGuard(Func<Task> action)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        return ExecuteAsync(action);

        async Task ExecuteAsync(Func<Task> handler)
        {
            if (IsBusy)
            {
                return;
            }

            await RunOnUiThreadAsync(() => IsBusy = true).ConfigureAwait(false);

            try
            {
                await handler().ConfigureAwait(false);
            }
            finally
            {
                await RunOnUiThreadAsync(() => IsBusy = false).ConfigureAwait(false);
            }
        }
    }

    private Task RunOnUiThreadAsync(Action action)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        if (_dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return _dispatcher.InvokeAsync(action);
    }

    private void UpdateCommandStates()
    {
        if (_dispatcher.CheckAccess())
        {
            _addCustomerCommand.RaiseCanExecuteChanged();
            _saveCustomerCommand.RaiseCanExecuteChanged();
            _addProjectCommand.RaiseCanExecuteChanged();
            _saveProjectCommand.RaiseCanExecuteChanged();
            _refreshCommand.RaiseCanExecuteChanged();
        }
        else
        {
            _dispatcher.Post(UpdateCommandStates);
        }
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed record class CustomerItem(Guid Id, string Name, bool IsArchived)
{
    public string DisplayName => IsArchived ? $"{Name} (Archived)" : Name;
}

public sealed record class ProjectItem(Guid Id, Guid CustomerId, string Name, bool IsActive, string CustomerName)
{
    public string DisplayName => Name;
    public string Status => IsActive ? "Active" : "Inactive";
    public string StatusForeground => IsActive ? "#7FD27F" : "#D06C6C";
}
