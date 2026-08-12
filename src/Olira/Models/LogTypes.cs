namespace Olira;

/// <summary>
/// Customer-facing log types. Values match the platform log catalog.
/// Most verb-suffixed subtypes were renamed to noun-only canonical
/// names. Deprecated members remain valid indefinitely; prefer canonical names for
/// new integrations.
/// </summary>
public static class OliraLogType
{
    /// <summary>Symptom report.</summary>
    public const string SymptomReport = "symptom_report";

    /// <summary>Free-text symptom report.</summary>
    public const string SymptomFreeText = "symptom_free_text";

    /// <summary>Symptom detail.</summary>
    public const string SymptomDetail = "symptom_detail";

    /// <summary>Deprecated: use <see cref="MoodReport"/>.</summary>
    public const string MoodsReport = "moods_report";

    /// <summary>Deprecated: use <see cref="FunctionalClass"/>.</summary>
    public const string FunctionalClassReported = "functional_class_reported";

    /// <summary>Deprecated: use <see cref="HealthMetric"/>.</summary>
    public const string HealthMetricReported = "health_metric_reported";

    /// <summary>Deprecated: use <see cref="LabResults"/>.</summary>
    public const string LabResultsReceived = "lab_results_received";

    /// <summary>Vitals measurement.</summary>
    public const string VitalsMeasurement = "vitals_measurement";

    /// <summary>Deprecated: use <see cref="ClinicalNote"/>.</summary>
    public const string ClinicalNoteReceived = "clinical_note_received";

    /// <summary>Deprecated: use <see cref="ClinicalFinding"/>.</summary>
    public const string ClinicalFindingReported = "clinical_finding_reported";

    /// <summary>Deprecated: use <see cref="ProcedureResult"/>.</summary>
    public const string ProcedureResultReceived = "procedure_result_received";

    /// <summary>Deprecated: use <see cref="Procedure"/>.</summary>
    public const string ProcedurePerformed = "procedure_performed";

    /// <summary>Deprecated: use <see cref="GenomicVariant"/>.</summary>
    public const string GenomicVariantReported = "genomic_variant_reported";

    /// <summary>Deprecated: use <see cref="ImagingResult"/>.</summary>
    public const string ImagingResultReceived = "imaging_result_received";

    /// <summary>Deprecated: use <see cref="ClinicalMeasurement"/>.</summary>
    public const string ClinicalMeasurementReported = "clinical_measurement_reported";

    /// <summary>Deprecated: use <see cref="TreatmentResponseAssessment"/>.</summary>
    public const string TreatmentResponseAssessmentReported = "treatment_response_assessment_reported";

    /// <summary>Deprecated: use <see cref="ClinicalPlanItem"/>.</summary>
    public const string ClinicalPlanItemReported = "clinical_plan_item_reported";

    /// <summary>Deprecated: use <see cref="CareEncounter"/>.</summary>
    public const string CareEncounterReported = "care_encounter_reported";

    /// <summary>Deprecated: use <see cref="CareGoal"/>.</summary>
    public const string CareGoalReported = "care_goal_reported";

    /// <summary>Deprecated: use <see cref="Immunization"/>.</summary>
    public const string ImmunizationReported = "immunization_reported";

    /// <summary>Deprecated: use <see cref="AllergyIntolerance"/>.</summary>
    public const string AllergyIntoleranceReported = "allergy_intolerance_reported";

    /// <summary>Deprecated: use <see cref="FamilyHistory"/>.</summary>
    public const string FamilyHistoryReported = "family_history_reported";

    /// <summary>Deprecated: use <see cref="Device"/>.</summary>
    public const string DeviceReported = "device_reported";

    /// <summary>Deprecated: use <see cref="CareAction"/>.</summary>
    public const string CareActionLogged = "care_action_logged";

    /// <summary>Memory report.</summary>
    public const string MemoryReport = "memory_report";

    /// <summary>Deprecated: use <see cref="UnstructuredReport"/>.</summary>
    public const string UnstructuredReportReceived = "unstructured_report_received";

    /// <summary>Questionnaire response.</summary>
    public const string QuestionnaireResponse = "questionnaire_response";

    /// <summary>Questionnaire item response.</summary>
    public const string QuestionnaireItemResponse = "questionnaire_item_response";

    /// <summary>Deprecated: use <see cref="Conversation"/>.</summary>
    public const string ConversationCompleted = "conversation_completed";

    /// <summary>Deprecated: use <see cref="ConversationTurn"/>.</summary>
    public const string ConversationTurnLogged = "conversation_turn_logged";

    /// <summary>Deprecated: use <see cref="HeartRateData"/>.</summary>
    public const string HeartRateDataReceived = "heart_rate_data_received";

    /// <summary>Deprecated: use <see cref="SleepData"/>.</summary>
    public const string SleepDataReceived = "sleep_data_received";

    /// <summary>Deprecated: use <see cref="ActivityData"/>.</summary>
    public const string ActivityDataReceived = "activity_data_received";

    /// <summary>Deprecated: use <see cref="CgmReading"/>.</summary>
    public const string CgmReadingReceived = "cgm_reading_received";

    /// <summary>Deprecated: use <see cref="Spo2Reading"/>.</summary>
    public const string Spo2ReadingReceived = "spo2_reading_received";

    /// <summary>Deprecated: use <see cref="WeightMeasurement"/>.</summary>
    public const string WeightMeasurementReceived = "weight_measurement_received";

    /// <summary>Deprecated: use <see cref="MedicationListUpdate"/>.</summary>
    public const string MedicationAction = "medication_action";

    /// <summary>Deprecated: use <see cref="MedicationAdherence"/>.</summary>
    public const string MedicationDoseUpdate = "medication_dose_update";

    /// <summary>Deprecated: use <see cref="MedicationAdverseEvent"/>.</summary>
    public const string MedicationAdverseEventReported = "medication_adverse_event_reported";

    /// <summary>User login.</summary>
    public const string UserLogin = "user_login";

    /// <summary>User logout.</summary>
    public const string UserLogout = "user_logout";

    /// <summary>Deprecated: use <see cref="ContentInteraction"/>.</summary>
    public const string ContentInteracted = "content_interacted";

    /// <summary>Deprecated: use <see cref="NotificationInteraction"/>.</summary>
    public const string NotificationInteracted = "notification_interacted";

    /// <summary>Deprecated: use <see cref="TaskOutcome"/>.</summary>
    public const string TaskUpdated = "task_updated";

    /// <summary>Interaction feedback.</summary>
    public const string InteractionFeedback = "interaction_feedback";

    /// <summary>Deprecated: use <see cref="FeatureUsage"/>.</summary>
    public const string FeatureUsed = "feature_used";

    /// <summary>Deprecated: use <see cref="Demographics"/>.</summary>
    public const string DemographicsUpdated = "demographics_updated";

    /// <summary>Deprecated: use <see cref="Condition"/>.</summary>
    public const string ConditionRecorded = "condition_recorded";

    /// <summary>Deprecated: use <see cref="Preferences"/>.</summary>
    public const string PreferencesUpdated = "preferences_updated";

    /// <summary>Deprecated: use <see cref="EmergencyContact"/>.</summary>
    public const string EmergencyContactUpdated = "emergency_contact_updated";

    /// <summary>Deprecated: use <see cref="CareTeam"/>.</summary>
    public const string CareTeamUpdated = "care_team_updated";

    /// <summary>Deprecated: use <see cref="Insurance"/>.</summary>
    public const string InsuranceUpdated = "insurance_updated";

    /// <summary>Deprecated: use <see cref="SocialDeterminants"/>.</summary>
    public const string SocialUpdated = "social_updated";

    /// <summary>Deprecated: use <see cref="Pharmacy"/>.</summary>
    public const string PharmacyUpdated = "pharmacy_updated";

    /// <summary>Deprecated: use <see cref="TreatmentPhase"/>.</summary>
    public const string TreatmentPhaseChanged = "treatment_phase_changed";

    // Canonical noun-only names

    /// <summary>Mood report (canonical).</summary>
    public const string MoodReport = "mood_report";

    /// <summary>Functional class (canonical).</summary>
    public const string FunctionalClass = "functional_class";

    /// <summary>Health metric (canonical).</summary>
    public const string HealthMetric = "health_metric";

    /// <summary>Lab results (canonical).</summary>
    public const string LabResults = "lab_results";

    /// <summary>Clinical note (canonical).</summary>
    public const string ClinicalNote = "clinical_note";

    /// <summary>Clinical finding (canonical).</summary>
    public const string ClinicalFinding = "clinical_finding";

    /// <summary>Procedure result (canonical).</summary>
    public const string ProcedureResult = "procedure_result";

    /// <summary>Procedure (canonical).</summary>
    public const string Procedure = "procedure";

    /// <summary>Genomic variant (canonical).</summary>
    public const string GenomicVariant = "genomic_variant";

    /// <summary>Imaging result (canonical).</summary>
    public const string ImagingResult = "imaging_result";

    /// <summary>Clinical measurement (canonical).</summary>
    public const string ClinicalMeasurement = "clinical_measurement";

    /// <summary>Treatment response assessment (canonical).</summary>
    public const string TreatmentResponseAssessment = "treatment_response_assessment";

    /// <summary>Clinical plan item (canonical).</summary>
    public const string ClinicalPlanItem = "clinical_plan_item";

    /// <summary>Care encounter (canonical).</summary>
    public const string CareEncounter = "care_encounter";

    /// <summary>Care goal (canonical).</summary>
    public const string CareGoal = "care_goal";

    /// <summary>Immunization (canonical).</summary>
    public const string Immunization = "immunization";

    /// <summary>Allergy intolerance (canonical).</summary>
    public const string AllergyIntolerance = "allergy_intolerance";

    /// <summary>Family history (canonical).</summary>
    public const string FamilyHistory = "family_history";

    /// <summary>Device (canonical).</summary>
    public const string Device = "device";

    /// <summary>Care action (canonical).</summary>
    public const string CareAction = "care_action";

    /// <summary>Unstructured report (canonical).</summary>
    public const string UnstructuredReport = "unstructured_report";

    /// <summary>Conversation (canonical).</summary>
    public const string Conversation = "conversation";

    /// <summary>Conversation turn (canonical).</summary>
    public const string ConversationTurn = "conversation_turn";

    /// <summary>Heart rate data (canonical).</summary>
    public const string HeartRateData = "heart_rate_data";

    /// <summary>Sleep data (canonical).</summary>
    public const string SleepData = "sleep_data";

    /// <summary>Activity data (canonical).</summary>
    public const string ActivityData = "activity_data";

    /// <summary>CGM reading (canonical).</summary>
    public const string CgmReading = "cgm_reading";

    /// <summary>SpO2 reading (canonical).</summary>
    public const string Spo2Reading = "spo2_reading";

    /// <summary>Weight measurement (canonical).</summary>
    public const string WeightMeasurement = "weight_measurement";

    /// <summary>Medication list update (canonical).</summary>
    public const string MedicationListUpdate = "medication_list_update";

    /// <summary>Medication adherence (canonical).</summary>
    public const string MedicationAdherence = "medication_adherence";

    /// <summary>Medication adverse event (canonical).</summary>
    public const string MedicationAdverseEvent = "medication_adverse_event";

    /// <summary>Content interaction (canonical).</summary>
    public const string ContentInteraction = "content_interaction";

    /// <summary>Notification interaction (canonical).</summary>
    public const string NotificationInteraction = "notification_interaction";

    /// <summary>Task outcome (canonical).</summary>
    public const string TaskOutcome = "task_outcome";

    /// <summary>Feature usage (canonical).</summary>
    public const string FeatureUsage = "feature_usage";

    /// <summary>Demographics (canonical).</summary>
    public const string Demographics = "demographics";

    /// <summary>Condition (canonical).</summary>
    public const string Condition = "condition";

    /// <summary>Preferences (canonical).</summary>
    public const string Preferences = "preferences";

    /// <summary>Emergency contact (canonical).</summary>
    public const string EmergencyContact = "emergency_contact";

    /// <summary>Care team (canonical).</summary>
    public const string CareTeam = "care_team";

    /// <summary>Insurance (canonical).</summary>
    public const string Insurance = "insurance";

    /// <summary>Social determinants (canonical).</summary>
    public const string SocialDeterminants = "social_determinants";

    /// <summary>Pharmacy (canonical).</summary>
    public const string Pharmacy = "pharmacy";

    /// <summary>Treatment phase (canonical).</summary>
    public const string TreatmentPhase = "treatment_phase";
}
