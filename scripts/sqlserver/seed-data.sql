-- ============================================
-- EduPlatform SQL Server Seed Data
-- ============================================
-- Version: 1.0
-- Date: 2026-08-28
-- Description: Données de test pour le développement

USE EduPlatformDB;
GO

-- ============================================
-- 1. USERS
-- ============================================

-- Vérifier que les utilisateurs n'existent pas déjà
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'admin@eduplatform.com')
BEGIN
    INSERT INTO Users (Id, Email, PasswordHash, FirstName, LastName, Role, CreatedAt)
    VALUES 
    (
        '11111111-1111-1111-1111-111111111111',
        'admin@eduplatform.com',
        -- Password: Admin123! (hashé avec BCrypt)
        '$2a$12$fsl2GJqfdWHZ3/0QivHoT.zQxmoDGnCC6b.2BRCuy.MmWJpx7Z4b2',
        'Admin',
        'EduPlatform',
        'Admin',
        GETUTCDATE()
    );
    PRINT '? Admin user created (admin@eduplatform.com / Admin123!)';
END

IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'instructor@eduplatform.com')
BEGIN
    INSERT INTO Users (Id, Email, PasswordHash, FirstName, LastName, Role, CreatedAt)
    VALUES 
    (
        '22222222-2222-2222-2222-222222222222',
        'instructor@eduplatform.com',
        -- Password: Instructor123!
        '$2a$12$NhwqMO2m612WwBMIf7z6F.AX44I.xz7MtgCA6qDW1218ko2DUb7Y6',
        'Jean',
        'Dupont',
        'Instructor',
        GETUTCDATE()
    );
    PRINT '? Instructor user created (instructor@eduplatform.com / Instructor123!)';
END

IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'student@eduplatform.com')
BEGIN
    INSERT INTO Users (Id, Email, PasswordHash, FirstName, LastName, Role, CreatedAt)
    VALUES 
    (
        '33333333-3333-3333-3333-333333333333',
        'student@eduplatform.com',
        -- Password: Student123!
        '$2a$12$T3Me1qnknQ94Js3UmITpaevvO8JqWgJhbAN4CPMa83nYYQtEWu4UK',
        'Marie',
        'Martin',
        'Student',
        GETUTCDATE()
    );
    PRINT '? Student user created (student@eduplatform.com / Student123!)';
END

-- Utilisateurs additionnels
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'student2@eduplatform.com')
BEGIN
    INSERT INTO Users (Id, Email, PasswordHash, FirstName, LastName, Role, CreatedAt)
    VALUES 
    (
        NEWID(),
        'student2@eduplatform.com',
        '$2a$12$T3Me1qnknQ94Js3UmITpaevvO8JqWgJhbAN4CPMa83nYYQtEWu4UK',
        'Pierre',
        'Bernard',
        'Student',
        GETUTCDATE()
    );
    PRINT '? Additional student created';
END

UPDATE Users SET PasswordHash = '$2a$12$fsl2GJqfdWHZ3/0QivHoT.zQxmoDGnCC6b.2BRCuy.MmWJpx7Z4b2'
WHERE Email = 'admin@eduplatform.com';
UPDATE Users SET PasswordHash = '$2a$12$NhwqMO2m612WwBMIf7z6F.AX44I.xz7MtgCA6qDW1218ko2DUb7Y6'
WHERE Email = 'instructor@eduplatform.com';
UPDATE Users SET PasswordHash = '$2a$12$T3Me1qnknQ94Js3UmITpaevvO8JqWgJhbAN4CPMa83nYYQtEWu4UK'
WHERE Email IN ('student@eduplatform.com', 'student2@eduplatform.com');

-- ============================================
-- 2. COURSES
-- ============================================

DECLARE @InstructorId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222222';
DECLARE @Course1Id UNIQUEIDENTIFIER = '44444444-4444-4444-4444-444444444444';
DECLARE @Course2Id UNIQUEIDENTIFIER = '55555555-5555-5555-5555-555555555555';
DECLARE @Course3Id UNIQUEIDENTIFIER = '66666666-6666-6666-6666-666666666666';

-- Course 1: Big Data
IF NOT EXISTS (SELECT 1 FROM Courses WHERE Id = @Course1Id)
BEGIN
    INSERT INTO Courses (Id, Title, Description, Category, Level, DurationMinutes, ThumbnailUrl, InstructorId, CreatedAt, IsPublished)
    VALUES 
    (
        @Course1Id,
        'Introduction au Big Data',
        'Découvrez les fondamentaux du Big Data : Hadoop, MapReduce, HDFS et l''écosystème Hadoop. Ce cours vous donnera les compétences nécessaires pour commencer à travailler avec des données massives.',
        'BigData',
        'Debutant',
        360,
        'https://images.unsplash.com/photo-1551288049-bebda4e38f71',
        @InstructorId,
        GETUTCDATE(),
        1
    );
    PRINT '? Course "Introduction au Big Data" created';
END

-- Course 2: Intelligence Artificielle
IF NOT EXISTS (SELECT 1 FROM Courses WHERE Id = @Course2Id)
BEGIN
    INSERT INTO Courses (Id, Title, Description, Category, Level, DurationMinutes, ThumbnailUrl, InstructorId, CreatedAt, IsPublished)
    VALUES 
    (
        @Course2Id,
        'Machine Learning avec Python',
        'Apprenez les algorithmes de Machine Learning et leur implémentation avec Python et scikit-learn. Du preprocessing des données aux modèles de deep learning.',
        'IA',
        'Intermediaire',
        480,
        'https://images.unsplash.com/photo-1555949963-aa79dcee981c',
        @InstructorId,
        GETUTCDATE(),
        1
    );
    PRINT '? Course "Machine Learning" created';
END

-- Course 3: Développement Web
IF NOT EXISTS (SELECT 1 FROM Courses WHERE Id = @Course3Id)
BEGIN
    INSERT INTO Courses (Id, Title, Description, Category, Level, DurationMinutes, ThumbnailUrl, InstructorId, CreatedAt, IsPublished)
    VALUES 
    (
        @Course3Id,
        'ASP.NET Core et Blazor',
        'Créez des applications web modernes avec ASP.NET Core et Blazor. De l''API REST au frontend interactif, tout ce dont vous avez besoin pour devenir développeur full-stack .NET.',
        'WebDev',
        'Intermediaire',
        540,
        'https://images.unsplash.com/photo-1593720213428-28a5b9e94613',
        @InstructorId,
        GETUTCDATE(),
        1
    );
    PRINT '? Course "ASP.NET Core" created';
END

-- ============================================
-- 3. MODULES
-- ============================================

-- Modules pour le cours Big Data
IF NOT EXISTS (SELECT 1 FROM Modules WHERE CourseId = @Course1Id)
BEGIN
    INSERT INTO Modules (Id, Title, Description, VideoUrl, DurationMinutes, [Order], CourseId)
    VALUES 
    (
        NEWID(),
        'Introduction au Big Data',
        'Qu''est-ce que le Big Data ? Les 3 V et les cas d''usage',
        'https://www.youtube.com/watch?v=example1',
        45,
        1,
        @Course1Id
    ),
    (
        NEWID(),
        'Hadoop et HDFS',
        'Architecture Hadoop et le système de fichiers distribué HDFS',
        'https://www.youtube.com/watch?v=example2',
        60,
        2,
        @Course1Id
    ),
    (
        NEWID(),
        'MapReduce',
        'Le modèle de programmation MapReduce pour le traitement distribué',
        'https://www.youtube.com/watch?v=example3',
        75,
        3,
        @Course1Id
    ),
    (
        NEWID(),
        'Écosystème Hadoop',
        'Hive, Pig, HBase, Spark et autres outils de l''écosystème',
        'https://www.youtube.com/watch?v=example4',
        90,
        4,
        @Course1Id
    );
    PRINT '? Modules for "Introduction au Big Data" created';
END

-- Modules pour le cours Machine Learning
IF NOT EXISTS (SELECT 1 FROM Modules WHERE CourseId = @Course2Id)
BEGIN
    INSERT INTO Modules (Id, Title, Description, VideoUrl, DurationMinutes, [Order], CourseId)
    VALUES 
    (
        NEWID(),
        'Introduction au Machine Learning',
        'Les types d''apprentissage : supervisé, non-supervisé, par renforcement',
        'https://www.youtube.com/watch?v=example5',
        60,
        1,
        @Course2Id
    ),
    (
        NEWID(),
        'Preprocessing des données',
        'Nettoyage, normalisation, feature engineering',
        'https://www.youtube.com/watch?v=example6',
        75,
        2,
        @Course2Id
    ),
    (
        NEWID(),
        'Régression linéaire et logistique',
        'Premiers algorithmes de ML supervisé',
        'https://www.youtube.com/watch?v=example7',
        90,
        3,
        @Course2Id
    );
    PRINT '? Modules for "Machine Learning" created';
END

-- ============================================
-- 4. ENROLLMENTS
-- ============================================

DECLARE @StudentId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333333';

-- Inscription au cours Big Data
IF NOT EXISTS (SELECT 1 FROM Enrollments WHERE UserId = @StudentId AND CourseId = @Course1Id)
BEGIN
    INSERT INTO Enrollments (UserId, CourseId, EnrolledAt, [Plan], AmountPaid, PaymentStatus)
    VALUES 
    (
        @StudentId,
        @Course1Id,
        DATEADD(day, -7, GETUTCDATE()),
        'Free',
        0,
        'Completed'
    );
    PRINT '? Student enrolled in Big Data course';
END

-- Inscription au cours Machine Learning
IF NOT EXISTS (SELECT 1 FROM Enrollments WHERE UserId = @StudentId AND CourseId = @Course2Id)
BEGIN
    INSERT INTO Enrollments (UserId, CourseId, EnrolledAt, [Plan], AmountPaid, PaymentStatus)
    VALUES 
    (
        @StudentId,
        @Course2Id,
        DATEADD(day, -3, GETUTCDATE()),
        'Free',
        0,
        'Completed'
    );
    PRINT '? Student enrolled in ML course';
END

-- ============================================
-- 5. QUESTIONS (QCM)
-- ============================================

-- Questions pour le cours Big Data
IF NOT EXISTS (SELECT 1 FROM Questions WHERE CourseId = @Course1Id)
BEGIN
    INSERT INTO Questions (Id, Text, Options, CorrectAnswer, Points, CourseId)
    VALUES 
    (
        NEWID(),
        'Qu''est-ce que Hadoop ?',
        'Un framework Big Data,Un langage de programmation,Une base de données,Un système d''exploitation',
        'Un framework Big Data',
        1,
        @Course1Id
    ),
    (
        NEWID(),
        'Que signifie HDFS ?',
        'Hadoop Distributed File System,High Data File Storage,Hadoop Data Flow System,High Density File System',
        'Hadoop Distributed File System',
        1,
        @Course1Id
    ),
    (
        NEWID(),
        'Quelles sont les deux phases de MapReduce ?',
        'Map et Reduce,Split et Merge,Read et Write,Input et Output',
        'Map et Reduce',
        2,
        @Course1Id
    ),
    (
        NEWID(),
        'Quelle est la réplication par défaut dans HDFS ?',
        '2,3,4,5',
        '3',
        1,
        @Course1Id
    ),
    (
        NEWID(),
        'Quel outil de l''écosystème Hadoop permet d''écrire des requêtes SQL ?',
        'Hive,Pig,HBase,Sqoop',
        'Hive',
        2,
        @Course1Id
    );
    PRINT '? Questions for Big Data course created';
END

-- ============================================
-- 6. VERIFICATION
-- ============================================

PRINT '';
PRINT '============================================';
PRINT '  Seed Data Summary';
PRINT '============================================';

SELECT COUNT(*) AS TotalUsers FROM Users;
SELECT COUNT(*) AS TotalCourses FROM Courses;
SELECT COUNT(*) AS TotalModules FROM Modules;
SELECT COUNT(*) AS TotalEnrollments FROM Enrollments;
SELECT COUNT(*) AS TotalQuestions FROM Questions;

PRINT '';
PRINT '? Seed data inserted successfully!';
PRINT '';
PRINT 'Test Accounts:';
PRINT '  Admin:      admin@eduplatform.com / Admin123!';
PRINT '  Instructor: instructor@eduplatform.com / Instructor123!';
PRINT '  Student:    student@eduplatform.com / Student123!';
PRINT '';
