﻿using BlowTrial.Helpers;
using BlowTrial.Models;
using BlowTrial.Properties;
using MvvmExtraLite.Helpers;
using System.ComponentModel;
using System.Windows.Input;

namespace BlowTrial.ViewModel
{
    class RandomisedMessagesViewModel : WizardPageViewModel, IDataErrorInfo
    {
        #region fields
        RandomisedMessagesModel _messagesModel;
        const string examplePatient = "Aarav Madras (S89754)";
        const int exampleId = 3078;
        #endregion

        #region constructors
        public RandomisedMessagesViewModel(RandomisedMessagesModel model)
        {
            _messagesModel = model;
            DisplayName = Strings.RandomisedMessagesViewModel_DisplayName;
            if (InterventionInstructions == null)
            {
                InterventionInstructions = Strings.RandomisedMessagesViewModel_DefaultIntervention;
            }
            if (ControlInstructions == null)
            {
                ControlInstructions = Strings.RandomisedMessagesViewModel_DefaultControl;
            }
            if (DischargeExplanation == null)
            {
                DischargeExplanation = Strings.RandomisedMessagesViewModel_DefaultDischarge_Hospital;
            }

            SaveCmd = new RelayCommand(param => Save(), param => WasValidOnLastNotify);
            CancelCmd = new RelayCommand(param => CloseCmd.Execute(param));
        }
        #endregion

        #region properties
        public string EgInterventionDetails
        {
            get
            {
                return string.Format(Strings.NewPatient_ToIntervention, examplePatient, Strings.Vaccine_RussianBcg, exampleId);
            }
        }

        public string EgControlDetails
        {
            get
            {
                return string.Format(Strings.NewPatient_ToControl, examplePatient, exampleId);
            }
        }

        public string InterventionInstructions
        {
            get
            {
                return _messagesModel.InterventionInstructions;
            }
            set
            {
                if (_messagesModel.InterventionInstructions == value) { return; }
                _messagesModel.InterventionInstructions = value;
                NotifyPropertyChanged("InterventionInstructions");
            }
        }

        public string ControlInstructions
        {
            get
            {
                return _messagesModel.ControlInstructions;
            }
            set
            {
                if (_messagesModel.ControlInstructions == value) { return; }
                _messagesModel.ControlInstructions = value;
                NotifyPropertyChanged("ControlInstructions");
            }
        }

        public string DischargeExplanation
        {
            get
            {
                return _messagesModel.DischargeExplanation;
            }
            set
            {
                if (_messagesModel.DischargeExplanation == value) { return; }
                _messagesModel.DischargeExplanation = value;
                NotifyPropertyChanged("DischargeExplanation");
            }
        }

        public ICommand SaveCmd { get; private set; }
        public ICommand CancelCmd { get; private set; }

        #endregion

        #region Methods
        public override bool IsValid()
        {
            return _messagesModel.IsValid();
        }
        #endregion

        #region private methods
        void Save()
        {
            BlowTrialDataService.SetRandomisingMessages(InterventionInstructions, ControlInstructions, DischargeExplanation);
            CloseCmd.Execute(null);
        }
        #endregion

        #region IDataError implementation
        string IDataErrorInfo.Error { get { return null; } }

        string IDataErrorInfo.this[string propertyName]
        {
            get
            {
                string error = this.GetValidationError(propertyName);
                CommandManager.InvalidateRequerySuggested();
                return error;
            }
        }
        string GetValidationError(string propertyName)
        {
            return ((IDataErrorInfo)_messagesModel)[propertyName];
        }
        #endregion
    }
}
